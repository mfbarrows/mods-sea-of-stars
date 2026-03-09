# `SunboyShootQTESunballState.HasQTESuccess` — Analysis

## Summary

`HasQTESuccess` returns `true` if the primary player's `playerQTEResult.result`
is any success value (0/1/2), OR if any co-op additional player has a success result.
**For a solo session it reduces to: `return playerQTEResult.result <= 2`.**

Writing `+0xE0 = 0` (EQTEResult.SuccessPerfect) is **confirmed sufficient** to
make this method return `true`. No other conditions exist.

---

## Pointer Arithmetic Key

`param_1` = `this` (longlong base, byte-addressed)

| Expression | Byte offset | C# field |
|---|---|---|
| `*(uint *)(param_1 + 0xE0)` | `+0xE0` | `playerQTEResult.result` (EQTEResult as uint) |
| `*(char **)(param_1 + 0x140)` | `+0x140` | `additionalPlayersQTEState` List object |

---

## `SunballAdditionalPlayersQTEState` Struct Layout (Confirmed)

```csharp
public struct SunballAdditionalPlayersQTEState  // 12 bytes (with padding)
{
    public bool buttonPressed;  // 0x0
    public bool buttonReleased; // 0x1
    // 0x2–0x3: padding
    public EQTEResult result;   // 0x4  (int, 4 bytes)
    public bool done;           // 0x8
    // 0x9–0xB: padding
}
```

In the backing array: entries at `backingArray + 0x20`, stride `0xC`.
Reading as `ulonglong` at each entry: lower 32 bits = bytes 0–3 (booleans + padding),
upper 32 bits (`>> 0x20`) = `EQTEResult result`.

---

## `IsSuccess` Bit Mask Explained

```c
((uVar1 >> 0x20 & 0xfffffffd) == 0) || ((int)(uVar1 >> 0x20) == 1)
```

Let `r = EQTEResult` value:

| r | Name | `r & ~2 == 0`? | `r == 1`? | IsSuccess? |
|---|---|---|---|---|
| 0 | SuccessPerfect | ✅ (0&~2=0) | — | ✅ |
| 1 | SuccessBeforeEvent | ❌ (1&~2=1) | ✅ | ✅ |
| 2 | SuccessAfterEvent | ✅ (2&~2=0) | — | ✅ |
| 3 | FailTooEarly | ❌ | ❌ | ❌ |
| 4 | FailDidNoPress | ❌ | ❌ | ❌ |
| 5 | NoQTE | ❌ | ❌ | ❌ |

This is `EQTEResultExtension.IsSuccess()` expressed as arithmetic instead of
a switch — same semantics.

---

## Annotated C# Reconstruction

```csharp
private bool HasQTESuccess()
{
    // ── Primary player ────────────────────────────────────────────────────────
    EQTEResult primary = playerQTEResult.result;   // +0xE0

    if (primary.IsSuccess())   // i.e. result ∈ {0, 1, 2}
        return true;

    // ── Co-op: check additional players ──────────────────────────────────────
    var list = additionalPlayersQTEState;           // +0x140
    if (list == null)
        return false;

    for (int i = 0; i < list.Count; i++)
    {
        SunballAdditionalPlayersQTEState entry = list[i];
        if (entry.result.IsSuccess())
            return true;
    }

    return false;
}
```

---

## Call Site: Where is HasQTESuccess Used?

`HasQTESuccess` is private and not patched by any known external code.
Based on the method list, the most likely call site is `StateExecute` — which
dispatches per-step logic. It may also be called from `BeginShoot` to gate the
`DoQTESuccessFeedback` call, but BeginShoot's pseudocode showed a direct
`proj.level == sunballMaxLevel` guard instead. A Ghidra search in `StateExecute`
would confirm.

---

## Confirmed: `+0xE0 = 0` Is Sufficient

| Scenario | `playerQTEResult.result` | `HasQTESuccess` |
|---|---|---|
| Natural success (player released at peak) | 0 (SuccessPerfect) | ✅ true |
| Auto-release mod writes `+0xE0 = 0` | 0 (SuccessPerfect) | ✅ true |
| Player missed window / no release | 4 (FailDidNoPress) | ❌ false |
| StateEnter default (no mod) | 4 (FailDidNoPress) | ❌ false |

No other fields need to be written for `HasQTESuccess` to return `true`.

---

## Confirmed Final Mod Write Set (Unchanged)

```csharp
unsafe void AutoRelease(SunboyShootQTESunballState instance)
{
    byte* b = (byte*)instance.Pointer;
    *(int*)(b + 0xE0)    = 0;     // FailDidNoPress(4) → SuccessPerfect(0)
    *(bool*)(b + 0x134)  = true;  // playerInputDone — bypasses Phase 4 input check
    *(float*)(b + 0x128) = -1f;   // expire currentChargeStepDuration → BeginShoot next frame
}
```

---

## Full Automation — Next Steps

For "suppress UI, auto-start charging, auto-release at peak" the remaining
unknowns are the *charging start* path. The state enters `BeginDisplayInstructions`
then presumably waits for player input before calling `BeginCharge`. Two methods
need decompiling:

| Method | VA | Purpose | Needed for automation |
|---|---|---|---|
| `BeginIn` | `0x180B258F0` | Transitions to the "press A to start" phase | Understand what triggers BeginCharge |
| `UpdateIn` | `0x180B27C20` | Per-frame update during that phase | **This is likely where the "press A" gate lives** — the target to bypass |
| `BeginCharge` | `0x180B25A80` | Starts the charge phase (spawns sunball, sets timer) | May need direct call |

**Strategy for auto-start**: Prefix on `UpdateIn`, detect that the state is waiting
for input, and call `BeginCharge` directly (it's private but patchable via
`AccessTools.Method`). Or: Postfix on `BeginDisplayInstructions` → immediately call
`BeginCharge`. Decompile `UpdateIn` to confirm which of those is cleaner.

**Strategy for suppressing UI text**: Prefix on `BeginDisplayInstructions` returning
`false` (suppress original) is sufficient — zero field writes, pure display method.
