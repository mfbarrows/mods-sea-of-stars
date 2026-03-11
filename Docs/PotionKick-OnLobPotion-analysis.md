# Analysis: `PotionKick$$OnLobPotion`

## Background

`PotionKick.OnLobPotion` is called once per throw during the PotionKick combo (Seraï + Resh'an
collaborative move). It is registered as Seraï's `animEventHandler.onAttackHit` handler during
`DoMove`. Resh'an triggers the animation that fires the attack-hit event six times (once per
potion), calling this method each time.

This method is responsible for:
- Tracking throw count
- Spawning and launching the lob projectile from Seraï's throw socket
- **Immediately adding the projectile to `kickablePotions`** ← critical finding
- Registering a `TeamQTEResult` entry in `qteResultsByPotion`
- Subscribing `potionReachedSeraiCallback` to the projectile for when it arrives
- On the final throw: unsubscribing the animation event and starting cleanup

---

## Original Ghidra C

```c
void PotionKick$$OnLobPotion
               (longlong param_1,longlong *******param_2,longlong ******param_3,
               undefined1 (*param_4) [16])
{
  // ... (locals omitted for brevity — see full listing below) ...

  // one-time IL2CPP method cache init
  if (DAT_183a0f752 == '\0') {
    FUN_1802a4cc0(&Method$..Dictionary<Projectile,TeamQTEResult>.set_Item());
    FUN_1802a4cc0(&Method$..GetComponent<Projectile>());
    FUN_1802a4cc0(&Method$..List<Projectile>.Add());
    FUN_1802a4cc0(&Method$..ScriptableObjectByObjectPoolManager.get_Instance());
    FUN_1802a4cc0(&Method$..PoolManager.get_Instance());
    FUN_1802a4cc0(&Method$..PoolableClass<TeamQTEResult>.GetFromPool());
    FUN_1802a4cc0(&Sabotage.Pooling.PoolableClass<TeamQTEResult>_TypeInfo);
    FUN_1802a4cc0(&Trajectory_TypeInfo);
    DAT_183a0f752 = '\x01';
  }

  // 1. Increment throw count
  *(int *)(param_1 + 0xf8) = *(int *)(param_1 + 0xf8) + 1;
  uVar12 = *(uint *)(param_1 + 0xf8);          // potionThrowDoneCount (now incremented)
  lVar6 = *(longlong *)(param_1 + 0x100);       // reshan

  // 2. Select trajectory waypoint from reshan based on throw parity
  if (uVar12 == *(uint *)(param_1 + 0x88)) {    // last throw (== potionAmountToThrow)
    // read 8 floats from reshan+0x1C0 → local_28..uStack_c
    local_28 = *(undefined4 *)(lVar6 + 0x1c0);  // ...
    // (uStack_24..uStack_c = lVar6+0x1c4 through +0x1dc)
  }
  else if ((uVar12 & 1) == 0) {                 // even throw
    local_28 = *(undefined4 *)(lVar6 + 0x220);  // ...
  }
  else {                                         // odd throw (not last)
    local_28 = *(undefined4 *)(lVar6 + 0x200);  // ...
  }

  // 3. Resolve projectile prefab from pool manager
  ppppppplVar3 = FUN_1800126b0(ppppppplVar9, param_2, ...);   // get pool reference for count

  // 4. Spawn projectile GameObject from pool
  ppppppplVar4 = Sabotage.Pooling.PoolManager$$GetObjectInstance(
                    ppppppplVar3, *(param_1 + 0xb8), 0, 0);    // lobPotionProjectilePrefab

  // 5. Get Projectile component
  ppppppplVar3 = UnityEngine.GameObject$$GetComponent<Projectile>(ppppppplVar4, ...);

  // 6. Get projectile's Transform
  lVar6 = (*DAT_183a1fbb0)();                   // Component.get_transform()

  // 7. Look up Seraï's throw socket position
  ppppppplVar9 = *(param_1 + 0x100);            // reshan
  ppppppplVar9 = ppppppplVar9[0xf];             // CombatActor field
  param_3 = ppppppplVar9[7];                    // ImposterSocketPosition list
  param_3 = param_3[3];                         // specific socket
  param_2 = &local_28;                          // socket selector (from throw-parity block)
  puVar7 = ImposterSocketPosition$$GetPosition(ppppppplVar9, &local_28, param_3, 0);

  // 8. Set projectile spawn position
  local_58 = *puVar7;
  local_50 = *(puVar7 + 1);
  (*DAT_183a20460)(lVar6, &local_58);           // Transform.set_position(pos)

  // 9. Subscribe potionReachedSeraiCallback to onProjectileReachedTarget
  Projectile$$add_onProjectileReachedTarget(
      ppppppplVar3, *(param_1 + 0x130), 0, param_4);   // potionReachedSeraiCallback

  // 10. Build lob trajectory target position
  uVar1    = *(undefined4 *)(param_1 + 0x180);        // kickImpactPosition.z (or projectile target z)
  local_68 = CONCAT44(*(param_1+0x144), *(param_1+0x178));   // seraiPositioning.? + kickImpactPosition.x
  // (local_68 + local_50 form the Vector3 target position passed to RunTrajectory)

  // 11. Get trajectory instance and run it
  plVar8 = FUN_1800156b0(ppppppplVar9, ...);           // get trajectory pool manager
  ppppppplVar9 = ScriptableObjectByObjectPoolManager$$GetObjectInstance(
                    plVar8, *(param_1 + 0xc0), 0, 0);  // lobPotionTrajectory instance
  // type-check ppppppplVar9 is Trajectory
  local_48 = local_68;                                 // trajectory position struct
  local_40 = uVar1;
  Projectile$$RunTrajectory(ppppppplVar3, param_2, &local_48, 0);

  // *** CRITICAL: 12. Add projectile to kickablePotions IMMEDIATELY ***
  ppppppplVar9 = *(longlong ********)(param_1 + 0x198);   // kickablePotions
  if (ppppppplVar9 != 0) {
    FUN_180015cf0(ppppppplVar9, ppppppplVar3, param_3);    // List<Projectile>.Add(projectile)
  }

  // 13. Get TeamQTEResult from pool, register in qteResultsByPotion
  ppppppplVar4 = *(longlong ********)(param_1 + 0x1b0);   // qteResultsByPotion
  ppppppplVar9 = Sabotage.Pooling.PoolableClass<TeamQTEResult>$$GetFromPool(...);
  if (ppppppplVar4 != 0) {
    Dictionary<Projectile,TeamQTEResult>.set_Item(qteResultsByPotion, projectile, teamQTEResult);

    // 14. Not-last-throw early exit
    if (*(int *)(param_1 + 0xf8) < *(int *)(param_1 + 0x88)) {
      return;   // potionThrowDoneCount < potionAmountToThrow → more throws coming
    }

    // 15. Last throw only: unsubscribe lobPotionCallback from animEventHandler
    ppppppplVar9 = reshan.animEventHandler;
    PlayerAnimationEventHandler$$remove_onAttackHit(
        ppppppplVar9, *(param_1 + 0x128), 0, param_4);    // lobPotionCallback

    // 16. Last throw only: remove Seraï boost FX
    // (navigate reshan → liveManaHandler → PlayerCombatActorDependencies → boostController)
    CombatBoostLevelController$$RemoveBoostFX(boostController, ...);

    // 17. Last throw only: start RespawnPotionCoroutine
    param_4 = new PotionKick.<RespawnPotionCoroutine>d__72_TypeInfo();
    param_4.__this = param_1;     // coroutine state machine captures `this`
    UnityEngine.MonoBehaviour$$StartCoroutine(param_1, param_4, ...);
  }

LAB_180539c9a:
  FUN_1802845b0(...);  // NullReferenceException handler
}
```

---

## IL2CPP Parameter Mapping

```c
void PotionKick$$OnLobPotion(
    longlong param_1,   // this (PotionKick*)
    ...
)
```

---

## Field Offsets Referenced

All offsets on the `PotionKick` object (`param_1`):

| Offset | C# field | Type |
|---|---|---|
| `0x88` | `potionAmountToThrow` | `int` |
| `0xB8` | `lobPotionProjectilePrefab` | `GameObject` |
| `0xC0` | `lobPotionTrajectory` | `Trajectory` |
| `0xF8` | `potionThrowDoneCount` | `int` |
| `0x100` | `reshan` | `ReshanCombatActor` |
| `0x128` | `lobPotionCallback` | `Action` |
| `0x130` | `potionReachedSeraiCallback` | `Action<Projectile>` |
| `0x140` | `seraiPositioning` | `CombatPositioningResult` |
| `0x178` | `kickImpactPosition` | `Vector3` |
| `0x184` | `projectileAndTargetImpactPosition` | `Vector3` |
| `0x198` | `kickablePotions` | `List<Projectile>` |
| `0x1B0` | `qteResultsByPotion` | `Dictionary<Projectile, TeamQTEResult>` |

Offsets on `reshan` (ReshanCombatActor, itself a PlayerCombatActor):

| Offset | Role |
|---|---|
| `reshan+0x100` | `animEventHandler` (PlayerCombatActor.animEventHandler) |
| `reshan+0x1C0` | Throw waypoint data — last throw (8 floats / 32 bytes) |
| `reshan+0x200` | Throw waypoint data — odd throw |
| `reshan+0x220` | Throw waypoint data — even throw |

The 32-byte waypoint blocks are read as 8 `undefined4` values into `local_28..uStack_c`.
They are passed to `ImposterSocketPosition.GetPosition` as the socket selector — these
likely encode the alternating left/center/right lob positions producing the fan pattern.

---

## Equivalent C#

```csharp
private void OnLobPotion()
{
    potionThrowDoneCount++;

    // Select throw waypoint from Resh'an based on throw parity
    ThrowWaypointData waypoint;
    if (potionThrowDoneCount == potionAmountToThrow)
        waypoint = reshan.GetWaypointData_Last();    // reshan+0x1C0
    else if ((potionThrowDoneCount & 1) == 0)
        waypoint = reshan.GetWaypointData_Even();    // reshan+0x220
    else
        waypoint = reshan.GetWaypointData_Odd();     // reshan+0x200

    // Spawn projectile from pool
    var go = PoolManager.Instance.GetObjectInstance(lobPotionProjectilePrefab);
    var projectile = go.GetComponent<Projectile>();

    // Position at Seraï's throw socket
    var spawnSocket = ImposterSocketPosition.GetPosition(waypoint, ...);
    projectile.transform.position = spawnSocket;

    // Subscribe potionReachedSeraiCallback
    projectile.onProjectileReachedTarget += potionReachedSeraiCallback;

    // Build trajectory target = kickImpactPosition area + seraiPositioning
    var target = new LobTarget(seraiPositioning, kickImpactPosition, projectileAndTargetImpactPosition);
    var trajectoryInst = ScriptableObjectByObjectPoolManager.Instance.GetObjectInstance(lobPotionTrajectory);
    projectile.RunTrajectory(trajectoryInst, target);

    // Immediately add to kickable list and QTE registry
    kickablePotions.Add(projectile);                           // ← CRITICAL: synchronous, before return
    var teamQTE = PoolableClass<TeamQTEResult>.GetFromPool();
    qteResultsByPotion[projectile] = teamQTE;

    // For all throws except the last: done
    if (potionThrowDoneCount < potionAmountToThrow)
        return;

    // Last throw only: unsubscribe animation event, clean up boost FX, start respawn
    reshan.animEventHandler.onAttackHit -= lobPotionCallback;
    reshan.liveManaHandler.GetDependencies().boostController.RemoveBoostFX();
    StartCoroutine(RespawnPotionCoroutine());
}
```

---

## Key Observations

### 1. `kickablePotions.Add` is SYNCHRONOUS and happens INSIDE `OnLobPotion` ← CRITICAL

The potion is added to `kickablePotions` (and `qteResultsByPotion`) **before `OnLobPotion`
returns**, while the projectile arc is still beginning. This means by the time any Postfix
patch fires, both data structures are already populated.

This **overturns the earlier hypothesis** that `kickablePotions` was populated by the
`potionReachedSeraiCallback` (i.e., when the potion physically arrives at Seraï). The
callback fires later but does something else — its exact body is still unknown.

**Implication for Attempt 1:** Our `OnKick()` call in the Postfix had a populated
`kickablePotions`. The failure was NOT caused by an empty list. The cause must lie in how
`UpdateKicks` / `ProcessSuccessfulKicks` behaves in single-player mode (no SPP players).

### 2. Throw waypoints control the alternating fan pattern

The three waypoint blocks at `reshan+0x1C0` (last), `reshan+0x200` (odd), `reshan+0x220`
(even) produce the alternating throw positions. Potions 1, 3, 5 use the odd block; 2, 4 use
the even block; the 6th uses the last block.

### 3. `potionReachedSeraiCallback` is subscribed but its body is unknown

Each spawned projectile gets `potionReachedSeraiCallback` attached to its
`onProjectileReachedTarget` event. This fires when the arc reaches its target. Since
`kickablePotions` is already populated at throw time, this callback presumably does
something else — possibly triggering an animation, sound, or updating `kickImpactPosition`.
Decompile the callback body to confirm.

### 4. Last-throw cleanup starts `RespawnPotionCoroutine`

Only when `potionThrowDoneCount == potionAmountToThrow` does the method:
- Unsubscribe `lobPotionCallback` from Seraï's attack-hit event
- Remove the boost FX
- Start `RespawnPotionCoroutine`

The coroutine name implies that missed potions can be re-thrown. This is unrelated to
auto-timing but worth noting.

### 5. `qteResultsByPotion` uses pooled `TeamQTEResult` objects

The `TeamQTEResult` for each potion is obtained from the `PoolableClass<TeamQTEResult>` pool
rather than allocated fresh. This means results carry no leftover data from previous
encounters — pooled objects are expected to be reset on retrieval.

---

## Open Questions

| Question | Where to look |
|---|---|
| What does `potionReachedSeraiCallback` do? | Decompile the method it points to (likely a lambda captured in `DoMove`, stored in field `0x130`) |
| Why did Attempt 1 fail if `kickablePotions` was populated? | Decompile `ProcessSuccessfulKicks` — it must handle single-player kicks differently from SPP |
| What is the lob trajectory target exactly? | `CONCAT44(*(0x144), *(0x178))` — first field of `seraiPositioning` + `kickImpactPosition.x` — is this Seraï's position or the enemy? |
