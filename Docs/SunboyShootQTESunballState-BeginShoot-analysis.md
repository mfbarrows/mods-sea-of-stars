# `SunboyShootQTESunballState.BeginShoot` — Analysis

## Role

`BeginShoot` is the final step of the charging QTE. It is called from
`UpdateCharging` once `currentChargeStepDuration <= 0` and `playerInputDone == true`.

It assembles a `TeamQTEResult` from all player inputs, conditionally fires the
success feedback effects, advances `currentStep`, and optionally calls `ThrowSunball`.

> **Important correction from pseudocode**: BeginShoot does NOT have a
> "suboptimal release" path that still sends the shot. If `proj.level !=
> sunballMaxLevel` when BeginShoot fires, the function falls through to
> `FUN_1802845b0` (IL2CPP fatal-error helper) — unreachable in normal play.
> `UpdateCharging` guarantees level == maxLevel by only letting `playerInputDone`
> be set while in the peak window.

---

## Pointer Arithmetic Key

`param_1` = `this : SunboyShootQTESunballState`

| Expression | Offset | C# field | Type |
|---|---|---|---|
| `param_1[0x13][5]` | `0x135` | `shotStarted` | bool |
| `param_1[0xe]` | `0xE0` | `playerQTEResult` | QTEResult (16 bytes) |
| `param_1[0xe] + 0` | `0xE0` | `playerQTEResult.result` | EQTEResult (int, 8-byte slot) |
| `param_1[0xe] + 8` | `0xE8` | `playerQTEResult.owner` | Player* |
| `param_1[0x14]` | `0x140` | `additionalPlayersQTEState` | List<SunballAdditionalPlayersQTEState> |
| `param_1[0x13] + 8` | `0x138` | `additionalPlayers` | List<Player> |
| `param_1[0x11] + 8` | `0x118` | `sunballProjectileInstance` | SunballProjectile* |
| `param_1[0x11]` (deref) | `0x110` | `sunboy` | SunboyCombatActor* |
| `param_1[0xb]` | `0xB0` | `sunballMaxLevel` | int |
| `param_1[0xb] + 8` | `0xB8` | `onSunballReady` | Action |
| `param_1[0xb][4]` | `0xB4` | `throwSunballWhenReady` | bool |
| `param_1[0x12]` | `0x120` | `currentStep` | EShootSunballStep |

For `SunballProjectile proj`:
- `*(int*)((longlong)proj + 0xCC)` = `proj.level` (int)

`FUN_1800127d0(proj, ...)` — called with the projectile, returns a
`PlayerCombatActor`. This is `((CombatProjectile)proj).owner` or similar —
the `PlayerCombatActor` that threw the sunball (Sunboy).

---

## `QTEResult` Struct Layout

```csharp
public struct QTEResult   // 16 bytes
{
    public EQTEResult result;  // +0x0  (stored as 8-byte slot in IL2CPP)
    public Player owner;       // +0x8
}
```

## `EQTEResult` Enum Values

| Value | Name | Meaning |
|---|---|---|
| 0 | `SuccessPerfect` | Full success (what the game writes on max-level release) |
| 1 | `SuccessBeforeEvent` | Success timed early |
| 2 | `SuccessAfterEvent` | Success timed late |
| 3 | `FailTooEarly` | Released too early |
| 4 | `FailDidNoPress` | Did not release |
| 5 | `NoQTE` | No QTE applicable |

`EQTEResultExtension.IsSuccess()` returns `true` for values 0, 1, 2.

---

## Annotated C# Reconstruction

```csharp
private void BeginShoot()
{
    // ── Idempotency guard ─────────────────────────────────────────────────────
    if (shotStarted) return;   // +0x135
    shotStarted = true;

    // ── Pool a TeamQTEResult ──────────────────────────────────────────────────
    TeamQTEResult teamResult = PoolableClass<TeamQTEResult>.GetFromPool();
    if (teamResult == null) { FatalError(); return; }

    // ── Add primary player's QTE result ──────────────────────────────────────
    // Copies the 16-byte struct at +0xE0/+0xE8 onto the stack, passes by pointer.
    teamResult.AddResult(playerQTEResult);   // +0xE0

    // ── Add additional players' QTE results (co-op) ───────────────────────────
    // additionalPlayersQTEState at +0x140; additionalPlayers at +0x138.
    // Entries are 12-byte value types packed at backingArray + 0x20, stride 0xC.
    // The EQTEResult comes from the low dword; Player pointer from high dword.
    for (int i = 0; i < additionalPlayersQTEState.Count; i++)
    {
        // Reconstruct a QTEResult from the packed entry:
        //   low  32 bits → EQTEResult
        //   high 32 bits → player index (used to look up additionalPlayers[i])
        teamResult.AddResult(additionalPlayersQTEState[i].GetQTEResult());
    }

    // ── Primary success condition: level == maxLevel ──────────────────────────
    SunballProjectile proj = sunballProjectileInstance;   // +0x118
    if (proj == null) { FatalError(); return; }

    if (proj.level == sunballMaxLevel)   // proj+0xCC vs this+0xB0
    {
        // Get the PlayerCombatActor that owns the projectile (= Sunboy).
        PlayerCombatActor owner = proj.GetOwner() as PlayerCombatActor;
        if (owner == null) { FatalError(); return; }

        // Per-player QTE starburst / text FX.
        CombatManager.Instance.DoQTESuccessFeedback(owner, sunboy, teamResult);

        // DoTeamTimedHitFeedback is gated:
        //   - Only fires if additionalPlayers.Count >= 1        (+0x138)
        //     OR CombatManager.someCoopFlag (+0x198) is true.
        // (Solo play: additionalPlayers is empty AND flag is false → skipped.)
        if (additionalPlayers.Count >= 1 || CombatManager.Instance.IsCoopEnabled)
        {
            CombatManager.Instance.DoTeamTimedHitFeedback(teamResult);
        }

        // Fire onSunballReady — notifies SolarRainCombatMove to check garlInPosition.
        onSunballReady?.Invoke();     // +0xB8

        // Advance state machine to "Shooting".
        currentStep = (EShootSunballStep)2;   // +0x120

        // If SolarRainCombatMove already signalled Garl is in position, throw now.
        if (throwSunballWhenReady)    // +0xB4
            ThrowSunball();

        return;
    }

    // proj.level < sunballMaxLevel — should never reach here in normal play.
    // UpdateCharging only sets playerInputDone while level == maxLevel (the
    // peak window). Reaching this line means a logic error upstream.
    FatalError();
}
```

---

## Success Condition Summary

`DoQTESuccessFeedback`, `DoTeamTimedHitFeedback`, `onSunballReady.Invoke()`, and
`ThrowSunball` all live **inside** the `if (proj.level == sunballMaxLevel)` block.
If that condition is false, BeginShoot falls to `FatalError`.

This means UpdateCharging's job is to ensure BeginShoot is **only ever called**
when level == maxLevel (by gating playerInputDone writes to the peak window).

| Release scenario | `proj.level == max`? | Success FX? | Shot fires? |
|---|---|---|---|
| Player releases at peak window | ✅ Yes | ✅ Yes | ✅ Yes |
| Auto-release mod fires at peak | ✅ Yes | ✅ Yes | ✅ Yes |
| Player releases below peak | ❌ No | ❌ No | ❌ FatalError (unreachable via normal UpdateCharging flow) |
| Peak window expires unmissed | ❌ No (already 3) | ❌ No | ❌ FatalError (unreachable) |

The "suboptimal shot" and "window expired fall-back shot" paths documented earlier
are handled entirely in `UpdateCharging`, before BeginShoot is ever called.
`UpdateCharging` routes those cases differently — it sets `shootOnNextChargeStep`
or directly calls `ThrowSunball` without ever calling `BeginShoot`.
BeginShoot is the **success-only** path.

---

## Additional Players Loop — Memory Layout (New from Pseudocode)

```c
// Ghidra pseudocode:
local_18 = *(ulonglong *)((longlong)param_2 + (longlong)(int)uVar10 * 0xc + 0x20);
param_4  = (longlong ******)(local_18 >> 0x20);   // high 32 bits
//   ... then:
local_18 = local_18 >> 0x20;
param_2  = &local_18;
TeamQTEResult$$AddResult(teamResult, param_2, 0, param_4);
```

The backing array of `additionalPlayersQTEState` stores packed 12-byte value types:
- **Base**: `backingArray + 0x20` (IL2CPP array element base)
- **Stride**: `0xC` = 12 bytes per entry
- **Layout** within each entry (speculative from bit-shift pattern):
  - bits 0–31: `EQTEResult` value (int)
  - bits 32–63: player reference or index (passed as `param_4`)
  - remaining 4 bytes: unknown (perhaps padding or a second int field)

This is a `SunballAdditionalPlayersQTEState` struct stored as a value type in an
IL2CPP `List<SunballAdditionalPlayersQTEState>` — no heap object per entry.

---

## `DoTeamTimedHitFeedback` Condition Gate (New from Pseudocode)

```c
// From Ghidra:
if (*(int *)(*(longlong *)(param_1[0x13] + 8) + 0x18) < 1) {
    // additionalPlayers.Count < 1 → check CombatManager flag
    lVar7 = CombatManager.get_Instance();
    if (*(char *)(lVar7 + 0x198) == '\0') goto LAB_180b26191;  // skip DoTeamTimedHitFeedback
}
CombatManager$$DoTeamTimedHitFeedback(cm, teamResult, ...);
LAB_180b26191:
// onSunballReady / currentStep / ThrowSunball continue here
```

The feedback only fires if **at least one of**:
- `additionalPlayers.Count >= 1` (co-op partner present), OR
- `*(byte *)(CombatManager + 0x198) != 0` (some CombatManager flag — likely multiplayer/network mode)

In a solo session: both will be false → `DoTeamTimedHitFeedback` is skipped entirely.
This does **not** affect damage or the timed-hit bonus — only the visual/audio FX.

The field `CombatManager + 0x198` is **not yet named** in the dump. Candidates:
`isCoopEnabled`, `isOnlineSession`, `hasTimedHitFX` — type is bool/byte.

---

The struct at `+0xE0` is the **only** source of the primary player's QTE result
in the `TeamQTEResult`. `TeamQTEResult.HasSuccess()` and `GetBestResult()` read
from the `results` list that `AddResult` populates.

`UpdateCharging` writes on successful release:
```c
*(int*)(base + 0xE0) = 0;  // EQTEResult.SuccessPerfect
// +0xE8 (owner) is NOT written here — StateEnter already set it to the correct player
```

**StateEnter pre-initialises `playerQTEResult.result = FailDidNoPress (4)` and
`playerQTEResult.owner = sunboy.player`.** For the auto-release mod the owner
write can be dropped. Only the `result` field needs to change from 4 → 0.

---

## Mod: Required Writes for Full Success

When `proj.level == sunballMaxLevel && !playerInputDone && !shootOnNextChargeStep`:

```csharp
unsafe void AutoRelease(SunboyShootQTESunballState instance)
{
    byte* b = (byte*)instance.Pointer;

    // 1. Upgrade result: FailDidNoPress(4) → SuccessPerfect(0)
    //    Owner (+0xE8) was pre-set by StateEnter — do NOT overwrite it.
    *(int*)(b + 0xE0) = 0;

    // 2. Mark input done — gates Phase 4 even while button is still held
    *(bool*)(b + 0x134) = true;            // playerInputDone

    // 3. Expire the step timer → BeginShoot fires on next UpdateCharging
    *(float*)(b + 0x128) = -1.0f;          // currentChargeStepDuration
}
```

---

## Control Flow Diagram

```
BeginShoot()
│
├─ shotStarted?  ──yes──→ return  (idempotency guard, +0x135)
│
├─ shotStarted = true
│
├─ GetFromPool<TeamQTEResult>()
│    └─ null? ──→ FatalError
│
├─ AddResult(playerQTEResult)          ← reads +0xE0 / +0xE8
│
├─ for i in additionalPlayersQTEState:
│    AddResult(entry[i].qteResult)     ← packed 12-byte stride at +0x140 backing array
│
├─ proj = sunballProjectileInstance    ← reads +0x118
│    └─ null? ──→ FatalError
│
├─ proj.level == sunballMaxLevel?      ← proj+0xCC vs this+0xB0
│   │
│   yes ─────────────────────────────────────────────────────────────────┐
│                                                                         │
│   owner = proj.GetOwner() as PlayerCombatActor                         │
│   null? ──→ FatalError                                                  │
│                                                                         │
│   DoQTESuccessFeedback(owner, sunboy, teamResult)                       │
│                                                                         │
│   additionalPlayers.Count >= 1  OR  cm.flag(+0x198)?                   │
│     yes ──→ DoTeamTimedHitFeedback(teamResult)                         │
│     no  ──→ (skip)                                                      │
│                                                                         │
│   onSunballReady.Invoke()            ← +0xB8                           │
│   currentStep = 2                    ← +0x120                          │
│   throwSunballWhenReady?  ──yes──→ ThrowSunball()                      │
│   return                             ◄──────────────────────────────────┘
│
└─ no  ──→ FatalError  (should be unreachable — UpdateCharging guards this)
```

---

## Remaining Unknown

`FUN_1800127d0(proj, ...)` — called twice in the success path to obtain a
`PlayerCombatActor` (or `CombatManager`). Most likely `Projectile.get_Owner()`
cast to `PlayerCombatActor`. Does not affect the mod — we don't invoke
`DoQTESuccessFeedback` manually; it fires naturally from the real code path.

`CombatManager + 0x198` — a single byte flag tested before `DoTeamTimedHitFeedback`.
Likely `isCoopEnabled` or `isOnlineSession`. Irrelevant for solo mod.


