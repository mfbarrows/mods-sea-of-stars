# `SinglePlayerPlusBlock.GetResult()` — Ghidra Analysis

VA `0x180D9ACF0` — confirmed match from dump.cs.

---

## Field offset map

### `param_2` = `this` (SinglePlayerPlusBlock)

| Ghidra expression | Byte offset | Field | Type |
|---|---|---|---|
| `*(char *)((longlong)param_2 + 0x44)` | `0x44` | `blocking` | `bool` |
| `param_2[10]` | `10 × 8 = 0x50` | `playerInputs` | `PlayerInputs` |
| `*(param_2[10] + 0x20)` | `0x20` into `PlayerInputs` | Rewired.Player reference | `Player` |

### `param_1` = caller-allocated return value (`QTEResult` struct)

| Offset | Field |
|---|---|
| `param_1[0]` / `*(undefined4*)param_1` | `EQTEResult result` (0x0) |
| `param_1[1]` | `Player owner` (0x8) |

### Vtable chain on `param_2`

Three chained virtual calls with short-circuit logic. The result (`uVar8`) is
only ever used as a parameter to the fatal-error handler — the grading path
ignores it entirely. These are almost certainly the same three vtable calls
seen in `TimedBlockHandler.GetResult()`:
`IsInputEnabled()` → `IsInputPhaseStarted()` → `<unknown>`.

---

## Annotated pseudocode

```c
QTEResult GetResult(SinglePlayerPlusBlock* self, ...)
{
    // Caller provides a zeroed QTEResult on the stack; we fill it in-place.
    QTEResult result = { 0, null };

    // ── Vtable chain (result unused by grade logic) ──────────────────────
    object context = self->vtable[0x1e0];
    bool ok = self->vtable[0x1d8](self);
    if (ok) {
        context = self->vtable[0x1f0];
        ok = self->vtable[0x1e8](self);
        if (ok) {
            context = self->vtable[0x200];
            self->vtable[0x1f8](self);
        }
    }
    // uVar8 / context only used below in the fatal-error path.

    // ── Grade computation (binary, identical to TimedBlockHandler) ────────
    result = { 0, null };   // clean slate (clobbers anything vtable wrote)

    result.result = self->blocking
        ? EQTEResult.SuccessBeforeEvent   // 1
        : EQTEResult.FailDidNoPress;      // 4

    // ── Owner assignment ──────────────────────────────────────────────────
    if (self->playerInputs == null)
        FatalError(4, context, ...);   // null playerInputs is unrecoverable

    result.owner = self->playerInputs.RewiredPlayer;  // *(playerInputs + 0x20)
    // IL2CPP GC write-barrier follows (LOCK/UNLOCK loop on DAT_183a6a380)

    return result;
}
```

---

## Approximate C# reconstruction

```csharp
// SinglePlayerPlusBlock : TimedInputHandler, IUpdatable
public QTEResult GetResult()
{
    // Vtable chain — purpose unclear, result not used for grading.
    // Likely IsInputEnabled / IsInputPhaseStarted checks for safety.
    if (IsInputEnabled()) {
        if (IsInputPhaseStarted()) {
            UnknownVirtualCall();
        }
    }

    // Binary grading — identical model to TimedBlockHandler.
    EQTEResult grade = blocking
        ? EQTEResult.SuccessBeforeEvent
        : EQTEResult.FailDidNoPress;

    // playerInputs must be non-null (assured by Init).
    Player player = playerInputs.RewiredPlayer;   // field at 0x20 in PlayerInputs

    return new QTEResult(owner: player, result: grade);
}
```

---

## Key conclusions

### 1. Grading model is identical to `TimedBlockHandler.GetResult()`

Same binary `blocking` check, same two possible outcomes:

| `blocking` | `EQTEResult` |
|---|---|
| `true` | `SuccessBeforeEvent` (1) |
| `false` | `FailDidNoPress` (4) |

### 2. Significantly simpler than `TimedBlockHandler.GetResult()`

No pool management (`TeamQTEResult` vs plain `QTEResult`), no
`AutoTimeBlockModifier` random check, no SinglePlayerPlusManager delegation.
This handler is already the "inner" per-player handler that
`SinglePlayerPlusManager.GetBlockResults()` collects from.

### 3. Our prefix patch is correct

Calling `DoBlock()` in a prefix sets `blocking = true` before the grade is
read — exactly the same reasoning as `TimedBlockHandler.GetResult()`.
`DoBlock()` is idempotent via `playingBlockAnimation` guard.

### 4. `TimedBlockHandler` vs `SinglePlayerPlusBlock` usage

`TimedBlockHandler` handles single-player + manages `TeamQTEResult` pooling
internally. `SinglePlayerPlusBlock` returns a raw `QTEResult` per player;
`TimedBlockHandler.GetResult()` (via `SinglePlayerPlusManager.GetBlockResults`)
aggregates them into a `TeamQTEResult` when SinglePlayerPlus mode is active.
The two patch files cover both paths.
