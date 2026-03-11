# Analysis: `KickPotionState$$UpdateKicking`

## Background

`KickPotionState` is Seraï's (player 1) state machine state during the kick window of the
PotionKick combo. `StateExecute` is called every frame while this state is active; it calls
`UpdateKicking` as part of its per-frame work.

---

## IL2CPP Parameter Mapping

```c
void KickPotionState$$UpdateKicking(
    longlong param_1,   // this (KickPotionState*)
    ...                 // method info / IL2CPP extras
)
```

---

## Field Offsets Referenced

From `KickPotionState` dump:

| Field | Offset | Type | C# name |
|---|---|---|---|
| `*(float *)(param_1 + 0x60)` | `0x60` | `float` | `kickEndTime` |
| `*(undefined1 *)(param_1 + 0x68)` | `0x68` | `bool` | `kicking` |

---

## Full Decompiled Body

```c
fVar3 = Time.get_time();            // UnityEngine.Time.time

if (kickEndTime <= fVar3) {         // kick animation has finished
    kicking = false;
}
```

That is the **entire method**. There are no other branches.

---

## Equivalent C#

```csharp
private void UpdateKicking()
{
    if (kickEndTime <= Time.time)
        kicking = false;
}
```

---

## What This Method Does (and Does Not Do)

### What it does
Clears the `kicking` flag once `Time.time` has passed `kickEndTime`.
`kickEndTime` is set elsewhere (almost certainly in `OnKickInput`) to
`Time.time + GetKickDuration()`, marking the end of the kick animation.

### What it does NOT do
- **No input detection.** There is no button-press check here.
- **No callback invocation.** `kickCallback` (= `PotionKick.OnKick`) is not called here.
- **No cooldown logic.** `nextKickValidTime` (0x64) is not touched here.

---

## Architectural Implication

`UpdateKicking` is purely a **timer drain** — a one-liner that expires the kick animation.
All real "kick accepted" logic happens elsewhere. The call chain must be:

```
StateExecute (every frame)
  ├── [input check] → if attack button pressed → (?)
  └── UpdateKicking() → clears kicking flag when animation ends
```

The `?` is unresolved. From the logs we know:
- `KickPotionState.OnKickInput` **never fired** even during confirmed manual kicks.
- `PotionKick.OnKick` (= `kickCallback`) **did fire** for every manual kick.

This means either:
1. `StateExecute` detects input and invokes `kickCallback` **directly** without calling
   `OnKickInput`, or
2. `OnKickInput` is called through an animation event or indirect path that our
   `[HarmonyPatch]` doesn't intercept correctly.

The most likely explanation is that `StateExecute` contains the input poll and directly
invokes `kickCallback`. `OnKickInput` may be called from an animation event or may only be
used for SPP input routing — it has a public visibility which supports this.

---

## Next Steps

Decompile `KickPotionState$$StateExecute` (RVA `0xA56130`) to see:
- How input is polled
- Whether `kickCallback` is invoked directly or via `OnKickInput`
- Whether `kicking == true` gates further input (cooldown guard)
- Whether `nextKickValidTime` is checked here
