# `TimedBlockHandler.OnInputPressed()` — Ghidra Analysis

RVA `0xD9D290` / VA `0x180D9D290` — confirmed match from dump.cs.

---

## Field offset map (`param_1` = `this`)

| Ghidra expression | Byte offset | Field | Type |
|---|---|---|---|
| `param_1[4]` | `4 × 8 = 0x20` | `playerInputs` *(base)* | `PlayerInputs` |
| `*(param_2 + 0x20)` | `0x20` into `PlayerInputs` | likely `Rewired.Player` reference | `Player` |

---

## Annotated pseudocode

```c
void OnInputPressed(TimedBlockHandler* self, ...)
{
    // Static class init for UIManager, UnityEngine.Object, BattleScreen
    // (IL2CPP lazy class initialisation, runs once)

    // Get UIManager singleton instance
    var uiManager = UIManager.get_Instance();
    if (uiManager == null) goto FatalError;

    // Get the BattleScreen UI view
    var battleScreen = uiManager.GetView<BattleScreen>();
    // (multiple null/type checks for UnityEngine.Object validity)

    if (battleScreen != null) {
        // Check if the timed-hit UI handle is active (IntPtr != IntPtr.Zero).
        // This is the UI overlay that shows the QTE timing indicator.
        // plVar3 = IntPtr.Zero sentinel from IntPtr_TypeInfo
        // lVar2 + 0x10  = a Handle/IntPtr field on the BattleScreen result
        bool uiHandleActive = (battleScreen.timedHitHandle != IntPtr.Zero);

        if (uiHandleActive) {
            // Get the Rewired.Player reference from playerInputs (offset 0x20)
            var playerInputs = self->playerInputs;             // param_1[4], offset 0x20
            var rewiredPlayer = *(playerInputs + 0x20);

            if (playerInputs != null && battleScreen.timedHitUI != null) {
                // Tell the timing UI to begin showing input feedback for this player.
                // This is purely visual — the ring/flash animation.
                TimedHitUI.BeginInputForPlayer(
                    battleScreen.timedHitUI,
                    rewiredPlayer,
                    false,
                    ...);
            }
        }

        // Always call DoBlock() if UIManager lookup succeeded.
        TimedBlockHandler.DoBlock(self, ...);
        return;
    }

FatalError:
    FUN_1802845b0(...);   // IL2CPP fatal null-ref handler
    swi(3);               // crash / abort
}
```

---

## Key conclusions

### 1. `OnInputPressed` does NO timing math

This is the most important finding. The entire function body is:
1. Get `BattleScreen` UI
2. If the timed-hit UI handle is active, call `TimedHitUI.BeginInputForPlayer` (visual feedback only)
3. Call `DoBlock()`

There is **no comparison against `Time.time`**, no window deadline check, no
`EQTEResult` assignment. The QTE grade is not decided here.

### 2. Contrast with the attack side

`AbstractTimedAttackHandler.OnInputPressed()` records a press timestamp and
computes `beforeHitWindowEndTime`. `TimedBlockHandler.OnInputPressed()` does
none of that. Blocks have a fundamentally different grading model.

### 3. Where the grade must come from

Since `DoBlock()` stores:
- `blockEndTime = now + GetBlockDuration()`
- `blockAnimationEndTime = blockEndTime − attackAnimEventTime`

And `GetResult()` (RVA `0xD9C9E0`) reads `inputResults` into a `TeamQTEResult`…

The grading almost certainly happens in **`GetResult()`** or
**`IUpdatableUpdate()`** by comparing the current time (or block start time)
against the attack animation event time. The `blockAnimationEndTime` field
is the key — it encodes *when relative to the attack event* the block fired.

### 4. Implication for the multi-press problem

Each call to `OnInputPressed` → `DoBlock()` overwrites `blockEndTime` and
`blockAnimationEndTime` with freshly computed values based on `Time.time`
at the moment of that call. **Later presses produce later timestamps**, which
means a later-computed `blockAnimationEndTime`. Whether that makes the grade
better or worse depends on `GetResult()`'s comparison logic.

This means the multi-press spam from our `GetInputDown=true` patch is:
- Calling `DoBlock()` repeatedly throughout the whole block window
- Each call updating `blockAnimationEndTime` to a different value
- The final `GetResult()` call uses whatever `blockAnimationEndTime` was last written

The last press before `GetResult()` is called determines the grade — which
is effectively random timing, explaining the "mixed results."

---

## What to look at next

- **`GetResult()` (RVA `0xD9C9E0`)** — contains the actual grading logic that
  reads `blockAnimationEndTime` and produces an `EQTEResult`.
- **`IUpdatableUpdate()` (RVA `0xD9C410`)** — to confirm whether multi-press
  is possible or if there is a `blocking` guard that prevents it.
- **`CanPressInput()` (RVA `0xD9D4D0`)** — determines when `GetInputDown` is
  allowed to flow through to `OnInputPressed`; understanding its reopen
  condition would let us prevent the multi-press entirely.

The cleanest fix, once `GetResult()` is understood, may be to call
`DoBlock()` exactly once at the right time — or to understand what
`blockAnimationEndTime` value corresponds to `SuccessPerfect` and set it
directly via a Harmony prefix on `GetResult()`.
