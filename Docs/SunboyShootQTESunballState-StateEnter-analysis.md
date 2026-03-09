# `SunboyShootQTESunballState.StateEnter` — Analysis

## Pointer Arithmetic Key

`param_1` is typed `ulonglong*`, so `param_1[n]` = `*(base + n×8)`.
Byte-addressed casts like `(longlong)param_1 + 0x134` still refer to `base + 0x134`.

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1 + 0x22` (= `puVar1`) | `+0x110` | `sunboy` (SunboyCombatActor*) |
| `*(int *)(param_1 + 0x26)` | `+0x130` | `instructionTypingDoneWaitLeft` (float) |
| `*(float*)((longlong)param_1 + 300)` | `+0x12C` | `sunballChargeDuration` (float) |
| `param_1[0x14]` | `+0xA0` | `instructionTypingDoneWait` (float) |
| `param_1[0x16]` | `+0xB0` | `sunballMaxLevel` (int) |
| `param_1[0xb]` as float | `+0x58` | `chargeStepDuration` (float) |
| `sunboyCombatActor + 0x1A0` | — | `sunboy.player` (Player*) |
| `param_1[0x1c]` | `+0xE0` | `playerQTEResult.result` (EQTEResult, 8-byte slot) |
| `param_1[0x1d]` | `+0xE8` | `playerQTEResult.owner` (Player*) |
| `*(undefined2*)((longlong)param_1 + 0x134)` | `+0x134` | `playerInputDone` + `shotStarted` (2-byte clear) |
| `*(undefined2*)(param_1 + 0x1b)` | `+0xD8` | `updateRecoil` + `updateEndPause` (2-byte clear) |
| `*(undefined1*)(param_1 + 0x1e)` | `+0xF0` | `inWaitingForInput` |
| `*(undefined1*)((longlong)param_1 + 0x124)` | `+0x124` | `shootOnNextChargeStep` |
| `*(undefined4*)(param_1 + 0x25)` | `+0x128` | `currentChargeStepDuration` (float) |
| `param_1[0x1a]` | `+0xD0` | `sunballCharge` (float) |

---

## Annotated C# Reconstruction

```csharp
public override void StateEnter()
{
    // ── 1. Acquire SunboyCombatActor ──────────────────────────────────────────
    sunboy = GetComponent<SunboyCombatActor>();   // +0x110
    // null → FatalError (guarded at the end)

    // ── 2. Derive sunballChargeDuration ──────────────────────────────────────
    // Total charge time = (maxLevel − 1) steps × step duration.
    // e.g. maxLevel=4, stepDuration=1.0 → 3.0 s (steps 1→2, 2→3, 3→4).
    // The hold window at max (qteFullChargeStepDuration) is NOT included here;
    // it's loaded separately when level reaches max in UpdateCharging.
    sunballChargeDuration = (sunballMaxLevel - 1) * chargeStepDuration;   // +0x12C

    // ── 3. Initialise instructionTypingDoneWaitLeft ──────────────────────────
    instructionTypingDoneWaitLeft = instructionTypingDoneWait;   // +0x130 ← +0xA0

    // ── 4. Resolve player ────────────────────────────────────────────────────
    Player player = sunboy.player;                // sunboy+0x1A0
    if (player == null)
        player = InputManager.Instance.FirstPlayer;

    // ── 5. CRITICAL: initialise playerQTEResult to the DEFAULT FAILURE state ─
    playerQTEResult.result = EQTEResult.FailDidNoPress;   // +0xE0 = 4
    playerQTEResult.owner  = player;                      // +0xE8

    // ── 6. Clear all runtime state flags ────────────────────────────────────
    playerInputDone    = false;   // +0x134  ┐ cleared as one 2-byte write
    shotStarted        = false;   // +0x135  ┘
    updateRecoil       = false;   // +0xD8   ┐ cleared as one 2-byte write
    updateEndPause     = false;   // +0xD9   ┘
    inWaitingForInput  = false;   // +0xF0
    shootOnNextChargeStep = false; // +0x124
    currentChargeStepDuration = 0.0f;  // +0x128  → Phase 3 fires on frame 1
    sunballCharge      = 0.0f;   // +0xD0

    // ── 7. Co-op setup ───────────────────────────────────────────────────────
    GatherAdditionalPlayers();

    // ── 8. Begin UI ──────────────────────────────────────────────────────────
    BeginDisplayInstructions();

    // ── 9. Animator setup ────────────────────────────────────────────────────
    // Freeze animator, play idle, step one frame so first pose is correct.
    sunboy.animator.speed = 0f;
    sunboy.animator.Play(SunboyAnims.idleHash, -1);
    sunboy.animator.Update(0f);
}
```

---

## Key Finding: `playerQTEResult.result` starts as `FailDidNoPress = 4`

```c
param_1[0x1c] = 4;       // playerQTEResult.result = EQTEResult.FailDidNoPress
param_1[0x1d] = uVar3;   // playerQTEResult.owner  = player
```

**StateEnter pre-populates `playerQTEResult.owner` with the correct player pointer.**
**The `result` field starts at `4` (FailDidNoPress), not `0`.**

`UpdateCharging` Phase 4 overwrites only `result` — not `owner` — when the player
releases at max level:
```c
// UpdateCharging Phase 4 (on successful release only):
*(int*)(base + 0xE0) = 0;  // EQTEResult.SuccessPerfect
// +0xE8 is NOT re-written because owner is already correct from StateEnter
```

---

## Impact on the Mod

### Before (from UpdateCharging analysis — now corrected)

We previously thought the mod had to write both `+0xE0` (result) **and** `+0xE8` (owner).

### Correct picture

| Field | Set by | Value | Mod must write? |
|---|---|---|---|
| `playerQTEResult.result` (+0xE0) | StateEnter: `FailDidNoPress=4`, UpdateCharging: `SuccessPerfect=0` on success | Default **4** | **Yes** — change 4→0 |
| `playerQTEResult.owner` (+0xE8) | StateEnter only | Correct player ptr, never garbage | **No** — already set |
| `playerInputDone` (+0x134) | StateEnter: `false`, UpdateCharging Phase 4: `true` | Default **false** | Yes |
| `currentChargeStepDuration` (+0x128) | StateEnter: `0.0`, UpdateCharging: countdown | Countdown in progress | Yes — set to -1.0f to expire |

The owner write can be dropped from the mod. The minimal correct write set:

```csharp
unsafe void AutoRelease(SunboyShootQTESunballState instance)
{
    byte* b = (byte*)instance.Pointer;

    // 1. Upgrade result from FailDidNoPress(4) → SuccessPerfect(0)
    *(int*)(b + 0xE0) = 0;

    // 2. Gate Phase 4 — prevents re-processing while the button is still held
    *(bool*)(b + 0x134) = true;   // playerInputDone

    // 3. Expire the step timer → BeginShoot fires on next UpdateCharging
    *(float*)(b + 0x128) = -1.0f; // currentChargeStepDuration
}
```

---

## `currentChargeStepDuration = 0` on entry — Boot behaviour

Setting this to `0.0f` means Phase 3 (`if (currentChargeStepDuration <= 0)`) fires
**immediately on the very first `UpdateCharging` frame**. This instantly steps the
level from its spawned initial value to 1, loading the first step timer. It's the
kick-start that gets the charge sequence rolling without a separate init call.

---

## `sunballChargeDuration` Formula

```
sunballChargeDuration = (sunballMaxLevel - 1) × chargeStepDuration
```

This is the total rising-charge window. It's the divisor used in UpdateCharging
to normalise `sunballCharge` into a [0,1] range for audio/visuals. It does not
include `qteFullChargeStepDuration` (the hold window) because visuals are pinned
to `1.0f` once max level is reached.

---

## Player Resolution

```c
uVar3 = *(ulonglong *)(sunboyCombatActor + 0x1A0);  // sunboy.player (Player*)
if (uVar3 == 0)
    uVar3 = InputManager.get_FirstPlayer();          // solo fallback
```

`PlayerCombatActor.player` is at `+0x1A0`. `SunboyCombatActor` inherits this.
This is NOT `sunboy.get_PlayerInputs()` — it's the `Player` ScriptableObject/
identity object, which is what `QTEResult.owner` stores.

---

## Full Reset Summary

| Field | Offset | Reset value | Notes |
|---|---|---|---|
| `sunboy` | `+0x110` | GetComponent result | Acquired fresh on entry |
| `sunballChargeDuration` | `+0x12C` | `(maxLevel-1) × stepDuration` | Computed |
| `instructionTypingDoneWaitLeft` | `+0x130` | `= instructionTypingDoneWait` | Copy |
| `playerQTEResult.result` | `+0xE0` | `4` (FailDidNoPress) | **Default = fail** |
| `playerQTEResult.owner` | `+0xE8` | `sunboy.Player` | Pre-populated |
| `playerInputDone` | `+0x134` | `false` | |
| `shotStarted` | `+0x135` | `false` | |
| `updateRecoil` | `+0xD8` | `false` | |
| `updateEndPause` | `+0xD9` | `false` | |
| `inWaitingForInput` | `+0xF0` | `false` | |
| `shootOnNextChargeStep` | `+0x124` | `false` | |
| `currentChargeStepDuration` | `+0x128` | `0.0f` | Triggers Phase 3 on frame 1 |
| `sunballCharge` | `+0xD0` | `0.0f` | |
