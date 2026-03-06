# `MoonrangSpecialMove$$OnProjectileReachedEnemy` — Ghidra Analysis

**VA**: `0x1805C7400`  
**RVA**: `0x5C7400`  
**Class**: `MoonrangSpecialMove`  
**C# signature** (from dump.cs): `private void OnProjectileReachedEnemy(MoonrangProjectile projectile)`

---

## Parameter Mapping

Ghidra decompiles this as a 4-parameter flat IL2CPP function. Using pointer-arithmetic stride rules
(each `longlong ******` index step = 8 bytes):

| Ghidra param | C# identity |
|---|---|
| `param_1` | `MoonrangSpecialMove* this` |
| `param_2` | `MoonrangProjectile* projectile` |
| `param_3` / `param_4` | IL2CPP hidden args (MethodInfo/misc) |

---

## Field Offset Cross-Reference

Pointer arithmetic: `param_1 + N` = byte offset `N × 8`.

| Ghidra expression | Byte offset | dump.cs field |
|---|---|---|
| `param_1[3]` | +0x18 | parent-class field — likely `CombatMove.owner` or similar |
| `param_1[6]` | +0x30 | `CombatMove.multiHitHandler` (`MultiHitHandler*`) |
| `param_1[0xB]` | +0x58 | parent class — `CombatMove.owner` as `CombatActor*` (used as attacker) |
| `param_1[0x17]` | +0xB8 | `MoonrangSpecialMove.hitEnemySFX` (`Event*`, Wwise) |
| `*(int *)(param_1 + 0x28)` | **+0x140** | **`MoonrangSpecialMove.hitCount`** (`private int`) ← incremented here |
| `param_1[0x27]` | +0x138 | `MoonrangSpecialMove.lastPlayerBounce` (`CombatActor*`) |
| `param_2[0x1b]` | +0xD8 | `MoonrangProjectile.currentTarget` (`CombatTarget*`) |

### HitData field offsets (direct byte addressing via `(longlong)pppppplVar4 + offset`)

| Ghidra expression | Byte offset | dump.cs field |
|---|---|---|
| `pppppplVar4[9]` | +0x48 | `HitData.attacker` (`CombatActor*`) |
| `pppppplVar4[8]` | +0x40 | `HitData.target` (`CombatTarget*`) |
| `*(undefined1 *)(... + 0x14)` | +0x14 | `HitData.showNumber` (`bool`) → cleared to `false` |
| `*(undefined1 *)(... + 0x16)` | +0x16 | `HitData.aoeHit` (`bool`) → cleared to `false` |

---

## Vtable Call Mapping

Dump.cs `Slot:` numbers are **hexadecimal** and match Ghidra's `(*param_1)[0xNN]` indices directly.

| Ghidra index | Slot (hex) | dump.cs method |
|---|---|---|
| `(*param_1)[0x65]` | Slot 65 | `GetPlayerToBounceTo() : CombatActor` |
| `(*param_1)[0x66]` | Slot 66 | `GetTimeToReachPlayer(MoonrangProjectile, CombatActor) : float` |
| `(*param_1)[0xA7]` | Slot A7 | unknown parent-class virtual — used to compute player position Vector3 |
| `(*param_1)[0xA9]` | Slot A9 | unknown parent-class virtual — resolves `CombatActor` for next player leg |
| `(*param_1)[0xAB]` | Slot AB | unknown parent-class virtual — notifies projectile of next target |

> Slots A7/A9/AB are above `MoonrangSpecialMove`'s highest declared slot (0x70 = `OnMoongirlReturnedToSlot`)
> and therefore belong to an ancestor (likely `PlayerCombatMove` or `CombatMove`).

---

## Reconstructed C#

```csharp
private void OnProjectileReachedEnemy(MoonrangProjectile projectile)
{
    // 1. Get FX reference from move owner (pppppplVar11 = this[3] = parent owner field)
    var fxOwner = this[3]; // parent class actor ref

    // 2. Get projectile world position
    var t = projectile.GetComponent<Transform>(); // Component::get_transform()
    Vector3 projPos = t.position;                  // Transform::get_position_Injected()

    // 3. Play hit-enemy FX at projectile position
    FXManager.Instance.PlayFX(deflectFX, fxOwner, projPos, 0, 2 /*mode*/, 0f, 0f);

    // 4. Play Wwise hit-enemy SFX
    hitEnemySFX.Post(gameObject);

    // 5. *** INCREMENT hitCount ***
    hitCount++;  // MoonrangSpecialMove+0x140

    // 6. Resolve the current target from the projectile
    CombatTarget target = projectile.currentTarget;  // MoonrangProjectile+0xD8

    // 7. Initialise static TeamQTEResult type if needed
    // (static init guard: DAT_183a0fa43)
    TeamQTEResult qteResult = ProjectileQTEResult; // from TeamQTEResult TypeInfo default enum

    // 8. Get pooled HitData from MultiHitHandler
    HitData hitData = multiHitHandler.GetHitData(0);

    if (hitData != null)
    {
        // 9. Populate HitData
        hitData.attacker = owner as CombatActor; // param_1[0xB] = +0x58
        hitData.target   = target;               // projectile.currentTarget
        hitData.SetQTEResult(qteResult);
        hitData.showNumber = false; // +0x14
        hitData.aoeHit     = false; // +0x16

        // 10. Apply damage
        bool hitLanded = multiHitHandler.ImpactHitTarget(hitData);

        if (hitLanded)
        {
            // 11. Camera shake feedback
            CombatMove.DoAttackHitCamShake(hitData.target);

            // vtable[0x65] call — GetPlayerToBounceTo() — result discarded here
            // (likely a side-effect call, or Ghidra missed the return value)

            // 12. Apply visual/audio hit effects
            multiHitHandler.EffectHitTarget(hitData);

            // 13. Resolve CameraController and shake
            var cb = hitData.GetCameraBehaviour(); // via FUN_180012c00 + object chain
            if (cb != null)
                cb.Shake(this.throwShake /*param_1[0x12] = +0x90*/);

            // 14. Notify the projectile it has bounced
            MoonrangProjectile.OnBounce(projectile, 0 /*bounce type*/);

            // 15. Determine which player receives the projectile next
            CombatActor nextPlayer = GetPlayerToBounceTo_VTable_A9(); // vtable[0xA9]

            // 16. Store result in lastPlayerBounce (+0x138)
            lastPlayerBounce = nextPlayer;

            // 17. Notify projectile of next target (vtable[0xAB])
            SetNextProjectileTarget_VTable_AB(projectile, nextPlayer);

            // 18. Compute impact position on that player (vtable[0xA7])
            Vector3 playerImpactPos = GetProjectileImpactPlayerPosition_VTable_A7(
                nextPlayer, projectile);

            // 19. *** Send projectile back to the player ***
            projectile.GoToPlayer(nextPlayer, playerImpactPos,
                GetTimeToReachPlayer(projectile, nextPlayer) /* vtable[0x66] */);

            return;
        }
    }

    // Fall-through: null-safety error handler (FUN_1802845b0 + swi(3))
}
```

---

## Step-by-Step Execution Trace

```
OnProjectileReachedEnemy(projectile)
│
├─ [guard] DAT_183a0fa43 — static init for LevelManager, FXManager, TeamQTEResult_TypeInfo
│
├─ 1. get_transform(projectile) → get_position_Injected()   ← enemy-impact world position
│
├─ 2. FXManager$$PlayFX(deflectFX, fxOwner, projPos, …)
│
├─ 3. AK.Wwise.Event$$Post(hitEnemySFX, gameObject)
│
├─ 4. hitCount++ ──────────────────────────────────────────── ★ hitCount @ +0x140
│
├─ 5. ppppplVar8 ← projectile.currentTarget (+0xD8)
│
├─ 6. MultiHitHandler$$GetHitData(multiHitHandler, 0)
│     ├─ hitData.attacker ← move.owner  (+0x48 on HitData)
│     ├─ hitData.target   ← currentTarget (+0x40 on HitData)
│     ├─ HitData$$SetQTEResult(hitData, qteResult)
│     ├─ hitData.showNumber ← false  (+0x14)
│     └─ hitData.aoeHit    ← false  (+0x16)
│
├─ 7. MultiHitHandler$$ImpactHitTarget(multiHitHandler, hitData)
│     ├─ [if hit]
│     │   ├─ CombatMove$$DoAttackHitCamShake(…)
│     │   ├─ vtable[0x65](this)  ← GetPlayerToBounceTo (side-effect or Ghidra missed return)
│     │   ├─ MultiHitHandler$$EffectHitTarget(…)
│     │   ├─ CameraBehaviour$$Shake(…)
│     │   ├─ MoonrangProjectile$$OnBounce(projectile, 0)
│     │   ├─ nextPlayer ← vtable[0xA9](this)
│     │   ├─ lastPlayerBounce (+0x138) ← nextPlayer
│     │   ├─ vtable[0xAB](this, projectile, nextPlayer)
│     │   ├─ playerPos ← vtable[0xA7](this, nextPlayer, projectile)
│     │   └─ MoonrangProjectile$$GoToPlayer(projectile, nextPlayer, playerPos)  ← ★ bounce back
│     └─ return
│
└─ [no hit / null guard] → FUN_1802845b0 error handler
```

---

## Key Observations

### 1. `hitCount` is always incremented on entry
Before any null-checks or early returns, `*(int *)(param_1 + 0x28)` (`hitCount` at +0x140) is
incremented. Every successful arrival at an enemy bumps the counter regardless of QTE outcome.

### 2. No termination check in this function
There is **no** `hitCount >= qteSuccessHitCount` comparison here. `GoToPlayer` is called
unconditionally in the hit path.

### 3. `DeflectProjectile` also contains no termination check
`DeflectProjectile` (VA `0x1805C6EF0`, analysed in
[DeflectProjectile-analysis.md](DeflectProjectile-analysis.md)) likewise contains no cap
check. It picks the next target from `availableTargetsLeft` (auto-refilled when empty) and
calls `GoToEnemy` unconditionally. Both sides of the bounce loop have now been decompiled
and neither terminates based on `hitCount`. The move strongly appears to loop until the
player misses a deflect.

The enemy-side handler (`OnProjectileReachedEnemy`) only applies damage + FX + sends it home.
The player-side handler (`OnProjectileReachedPlayer`) decides whether to continue or end.

### 4. `GoToPlayer` always fires in the success path
There is no conditional around `GoToPlayer`. As long as `ImpactHitTarget` returns a hit AND the
camera lookup succeeds (the outer `lVar7` chain), `GoToPlayer` is called unconditionally. The
"redirect to player" always happens here; the "redirect to next enemy or stop" always happens in
`OnProjectileReachedPlayer`.

### 5. QTE result at this layer
`HitData$$SetQTEResult` is called with the `TeamQTEResult` type-info default (Ghidra shows
`TeamQTEResult_TypeInfo[0x17]`). This is the **enemy-hit** QTE result applied to the HitData for
this bounce's damage calculation — unrelated to the deflect QTE grade. The deflect QTE is evaluated
separately in `OnProjectileReachedPlayer` / `DeflectMoonrangState.GetQTEResult`.

### 6. `lastPlayerBounce` stores bounce routing state
`param_1[0x27]` = byte +0x138 = `MoonrangSpecialMove.lastPlayerBounce` receives the
`CombatActor` returned by vtable[0xA9]. Despite the field name suggesting history, it is written
here with the *next* player target before `GoToPlayer` is called, acting as routing state.

---

## Relationship to the Mod Patch

Our patch in `AutoTimeMoonrangPatches.cs`:

```csharp
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.GetQTEResult))]
static class Patch_DeflectMoonrangState_GetQTEResult
{
    static void Prefix(DeflectMoonrangState __instance)
    {
        __instance.OnDeflectProjectile(); // sets deflecting = true
    }
}
```

This patches `OnProjectileReachedPlayer`'s path (the deflect QTE evaluation), not this function.
`OnProjectileReachedEnemy` is unaffected by our patch — it runs normally and always sends the
projectile back to the player, where our patch then ensures the deflect is evaluated as a success.

---

## Related Analysis Files

| File | Function |
|---|---|
| [OnProjectileReachedPlayer-Moonerang-analysis.md](OnProjectileReachedPlayer-Moonerang-analysis.md) | Dispatcher + player-side handler (deflect QTE gate) |
| [GetResult-Block-analysis.md](GetResult-Block-analysis.md) | Block QTE grading (same binary/ternary pattern as deflect) |
| [DoBlock-analysis.md](DoBlock-analysis.md) | `DoBlock()` — sets `playingBlockAnimation` = true |
