# Moonerang Bounce Dispatch — Ghidra Analysis

Covers two private methods in `MoonrangSpecialMove` that together route each projectile
arrival to the correct handler:

1. **`OnProjectileReachedTarget`** (VA `0x1805C7380`) — dispatcher, calls one of the two below
2. **`OnProjectileReachedPlayer`** (VA `0x1805C7D80`) — deflect QTE evaluation *(primary analysis)*

Call chain:
```
MoonrangProjectile.DispatchOnReachedTargetEvent()   ← Slot 17, VA 0x1805C3B70
  → fires onMoonrangProjectileReachedTarget event
    → MoonrangSpecialMove.OnProjectileReachedTarget(projectile)
        ├── projectile.currentTarget.owner.IsPlayer() == false
        │     → OnProjectileReachedEnemy(projectile)
        └── IsPlayer() == true
              → OnProjectileReachedPlayer(projectile)   ← main analysis below
```

---

## `OnProjectileReachedTarget` — Dispatcher

VA: `0x1805C7380`  
Signature (dump): `protected void OnProjectileReachedTarget(MoonrangProjectile projectile)`

### Parameters

| IL2CPP param | Logical name | Type |
|---|---|---|
| `param_1` | `this` | `MoonrangSpecialMove*` |
| `param_2` | `projectile` | `MoonrangProjectile*` |
| `param_3` | — | unused |
| `param_4` | — | scratch / IL2CPP MethodInfo |

### Reconstruction

The entire function is a three-level null-guard then a single branch:

```csharp
protected void OnProjectileReachedTarget(MoonrangProjectile projectile)
{
    // MoonrangProjectile+0xD8 = currentTarget (CombatTarget)
    var target = projectile.currentTarget;
    if (target == null) { /* fatal: FUN_1802845b0 + swi(3) */ return; }

    // CombatTarget+0x80 = owner (CombatActor)
    var owner = target.owner;
    if (owner == null) { /* fatal */ return; }

    // Virtual call: CombatActor vtable[0x33] → IsPlayer() (returns bool)
    bool isPlayer = owner.IsPlayer();

    if (!isPlayer)
        OnProjectileReachedEnemy(projectile);
    else
        OnProjectileReachedPlayer(projectile);
}
```

### Key offsets confirmed

| Expression | Offset | Field | Type |
|---|---|---|---|
| `param_2[0x1b]` | `0xD8` on `MoonrangProjectile` | `currentTarget` | `CombatTarget` |
| `currentTarget[0x10]` | `0x80` on `CombatTarget` | `owner` | `CombatActor` |
| `(*owner)[0x33]` | vtable slot 0x33 on `CombatActor` | `IsPlayer()` | `bool` |

`CombatTarget.owner` at `+0x80` is confirmed directly from the dump field list.  
`IsPlayer()` is a non-virtual property on `CombatTarget` (`VA 0x180F1E5E0`) but the code
calls it through the `CombatActor` vtable here — the vtable[0x33] dispatch on `CombatActor`
is the same semantic check, consistent with `PlayerCombatActor` overriding it true and
`EnemyCombatActor` returning false.

### What this means for patching

This function is **not a patch target** — it contains no QTE logic. It is purely a type
dispatch router. Patching either branch destination (`OnProjectileReachedEnemy` or
`OnProjectileReachedPlayer`) is the right approach.

---

## `OnProjectileReachedPlayer` — Deflect QTE

VA: `0x1805C7D80`  
Signature (dump): `private void OnProjectileReachedPlayer(MoonrangProjectile projectile)`

---

## Parameters

| IL2CPP param | Logical name | Type |
|---|---|---|
| `param_1` | `this` | `MoonrangSpecialMove*` |
| `param_2` | `projectile` | `MoonrangProjectile*` |
| `param_3/4` | scratch | unused by caller |

---

## Step-by-step Reconstruction

### 0. Lazy static initialisation
```
if (DAT_183a0fa44 == 0) {
    init Method$Manager<CombatManager>.get_Instance()
    init Method$Manager<FXManager>.get_Instance()
    init Method$PoolableClass<TeamQTEResult>.GetFromPool()
    init PoolableClass<TeamQTEResult>_TypeInfo
    init Method$StateMachine.GetState<DeflectMoonrangState>()
    DAT_183a0fa44 = 1
}
```
Standard IL2CPP first-call method registration; safe to ignore for patching purposes.

---

### 1. Get a pooled `TeamQTEResult`
```csharp
TeamQTEResult qteResult = PoolableClass<TeamQTEResult>.GetFromPool();
```
`pppppplVar4` holds this for the rest of the function.

---

### 2. Resolve `DeflectMoonrangState` from the projectile's current player target
```csharp
// param_2[0x1d] = offset 0xE8 on MoonrangProjectile = currentPlayerTarget (CombatActor)
// currentPlayerTarget[0x10] = offset 0x80 on CombatActor = stateMachine
StateMachine stateMachine = projectile.currentPlayerTarget.stateMachine;

DeflectMoonrangState deflectState =
    stateMachine.GetState<DeflectMoonrangState>();   // null if not present
```

If either `currentPlayerTarget` or `stateMachine` is null, or the state doesn't exist, the
function falls through to a fatal-error call at the bottom (`FUN_1802845b0` + `swi(3)`).

---

### 3. Evaluate the QTE
```csharp
deflectState.GetQTEResult(qteResult);   // fills qteResult in-place
```
`DeflectMoonrangState.GetQTEResult` (VA `0x180A463B0`) reads the timing data recorded inside
the state and writes pass/fail per-player into `TeamQTEResult`.

```csharp
CombatManager cm = CombatManager.get_Instance();
cm.DoTeamTimedHitFeedback(qteResult);
```

---

### 4. Branch on `TeamQTEResult.HasSuccess`

#### FAIL branch (`HasSuccess == false`)
```csharp
FXManager fxm = FXManager.get_Instance();
Vector3 pos = this.transform.position;   // param_1[0x14] = this.deflectFX (offset 0xA0)
fxm.PlayFX(this.deflectFX, pos);
this.OnDeflectFailed(projectile);        // virtual, vtable+0x578
return;
```
`deflectFX` is the `public GameObject deflectFX` field at offset `0xA0` on
`MoonrangSpecialMove`. `OnDeflectFailed` is Slot 68 (virtual, overridden in `Soonrang`).

#### SUCCESS branch (`HasSuccess == true`)
```csharp
CombatManager.DoQTESuccessFeedback(cm, this.moongirl, this.moongirl, qteResult);
// this.moongirl = param_1[0x20] = offset 0x100 = MoongirlCombatActor moongirl

deflectState.deflectCount++;   // *(int*)(deflectState + 0x80)

if (deflectState.playingDeflectAnimation) {   // byte at offset 0x6E
    goto DISPATCH_DEFLECT;
}

// -- first-time setup for this deflect window --
float now = Time.time;
deflectState.deflectAnimationEndTime = now + 0.1f;  // offset 0x60
deflectState.deflectEndTime          = now + 0.1f;  // offset 0x64
deflectState.nextDeflectValidTime    = Time.time;   // offset 0x68 (second call)

deflectState.deflecting              = true;         // offset 0x6C
deflectState.playingDeflectAnimation = true;         // offset 0x6E

// play Moongirl's deflect animation
Animator anim = deflectState.combatActor[0xf];      // offset 0x70 → combatActor → anim
Animator.Play(anim, MoongirlAnims[deflect_anim_id], layer: 0);

DISPATCH_DEFLECT:
    ResetDeflectStates();       // virtual Slot 67, vtable+0x568 — no projectile arg
    DeflectProjectile(projectile);   // virtual Slot 62, vtable+0x518
    return;
```

`ResetDeflectStates` is called first to clear the deflect-window bookkeeping (animation
flags, timers) so the state is ready for the next incoming bounce.  
`DeflectProjectile` (virtual, overridden in `Soonrang`) routes the projectile to the next
enemy target.

---

## `DeflectMoonrangState` Field Map

These offsets are confirmed by cross-referencing the Ghidra byte arithmetic with the dump
field list (`StateMachineState` base = `0x50`):

| Offset | Field | Type |
|---|---|---|
| `0x50` | `deflectWindowDuration` | float |
| `0x54` | `deflectCooldown` | float |
| `0x58` | `blockTimeLeftToEndDeflectAnimation` | float |
| `0x5C` | `coopTimedHitWindowMultiplierReductionPerHit` | float |
| `0x60` | `deflectAnimationEndTime` | float |
| `0x64` | `deflectEndTime` | float |
| `0x68` | `nextDeflectValidTime` | float |
| `0x6C` | `deflecting` | bool |
| `0x6D` | `deflectEnabled` | bool |
| `0x6E` | `playingDeflectAnimation` | bool ← checked / set in success path |
| `0x70` | `combatActor` | `PlayerCombatActor` |
| `0x78` | `additionalPlayersDeflection` | `List<AdditionalPlayersMoonrangDeflection>` |
| `0x80` | `deflectCount` | int ← incremented on every success |

---

## Reconstructed C#: `OnProjectileReachedPlayer`

```csharp
// MoonrangSpecialMove — VA 0x1805C7D80
private void OnProjectileReachedPlayer(MoonrangProjectile projectile)
{
    // 1. Pool a fresh result container
    TeamQTEResult qteResult = PoolableClass<TeamQTEResult>.GetFromPool();

    // 2. Walk projectile → currentPlayerTarget → stateMachine → DeflectMoonrangState
    var player       = projectile.currentPlayerTarget;          // MoonrangProjectile+0xE8
    var sm           = player.stateMachine;                     // offset 0x80 on CombatActor
    var deflectState = sm.GetState<DeflectMoonrangState>();     // null → fatal crash
    if (deflectState == null) return;

    // 3. Score the QTE and broadcast feedback
    deflectState.GetQTEResult(qteResult);                       // reads deflecting flags → fills result
    CombatManager.Instance?.DoTeamTimedHitFeedback(qteResult);

    if (!qteResult.HasSuccess)
    {
        // FAIL: play the miss FX at this object's world position, then end the chain
        Vector3 pos = this.transform.position;
        FXManager.Instance?.PlayFX(this.deflectFX, pos);        // deflectFX = MoonrangSpecialMove+0xA0
        OnDeflectFailed(projectile);                            // virtual Slot 68, vtable+0x578
        return;
    }

    // SUCCESS
    CombatManager.Instance?.DoQTESuccessFeedback(this.moongirl, qteResult);
    deflectState.deflectCount++;                                // DeflectMoonrangState+0x80

    if (!deflectState.playingDeflectAnimation)                  // +0x6E — guard against re-entry
    {
        float now = Time.time;
        deflectState.deflectAnimationEndTime = now + 0.1f;      // +0x60
        deflectState.deflectEndTime          = now + 0.1f;      // +0x64
        deflectState.nextDeflectValidTime    = Time.time;       // +0x68 — separate call, so ~= now
        deflectState.deflecting              = true;            // +0x6C
        deflectState.playingDeflectAnimation = true;            // +0x6E

        Animator anim = deflectState.combatActor.animator;
        anim.Play(MoongirlAnims.Deflect, layer: 0);
    }

    // Reset timing bookkeeping, then route projectile to next enemy
    ResetDeflectStates();           // virtual Slot 67, vtable+0x568
    DeflectProjectile(projectile);  // virtual Slot 62, vtable+0x518
}
```

---

## Approximate C#: `DeflectMoonrangState.GetQTEResult`

Not yet decompiled in Ghidra (VA `0x180A463B0`), but it can be reconstructed from the field
map and the identical pattern seen in `TimedBlockHandler.GetResult()`:

```csharp
// DeflectMoonrangState — VA 0x180A463B0
public void GetQTEResult(TeamQTEResult result)
{
    // Primary player (Moongirl)
    QTEResult primary = deflecting   // +0x6C
        ? QTEResult.SuccessBeforeEvent
        : QTEResult.FailDidNoPress;
    result.SetResultForPlayer(combatActor.GetOwner(), primary);  // combatActor = +0x70

    // Co-op: additional players can also deflect
    foreach (AdditionalPlayersMoonrangDeflection add in additionalPlayersDeflection)  // +0x78
    {
        QTEResult addResult = add.Deflecting
            ? QTEResult.SuccessBeforeEvent
            : QTEResult.FailDidNoPress;
        result.SetResultForPlayer(add.Player, addResult);
    }
}
```

**Key implication for patching:** `GetQTEResult` reads `deflecting` (field `+0x6C`). Setting
`deflecting = true` on the `DeflectMoonrangState` *before* `GetQTEResult` is called is
sufficient to guarantee `SuccessBeforeEvent` for the primary player. `OnDeflectProjectile()`
(VA `0x180A465A0`) is the public method that already does this — it is the natural
patch target (prefix on `GetQTEResult` that first calls `state.OnDeflectProjectile()`).

The `coopTimedHitWindowMultiplierReductionPerHit` field (`+0x5C`) and
`AdditionalPlayersMoonrangDeflection.Deflecting` cover the co-op case — both default to not
deflecting unless driven by actual input, so patching `OnDeflectProjectile()` only guarantees
the host player's result. A complete fix also needs `add.OnDeflectInput()` for each
`additionalPlayersDeflection` entry.

---

## Loop Termination — can this repeat infinitely?

**Strongly suggests Option 1 — loops until failure.** `DeflectProjectile`
(VA `0x1805C6EF0`, now fully analysed in
[DeflectProjectile-analysis.md](DeflectProjectile-analysis.md)) contains **no**
`hitCount >= qteSuccessHitCount` comparison. It picks the next enemy from
`availableTargetsLeft` (auto-refilling when empty) and calls `GoToEnemy` unconditionally.
Combined with `OnProjectileReachedEnemy` also containing no cap check, the evidence strongly
supports:

> **The Moonerang bounce chain loops indefinitely until the player misses a deflect.**

The bounce cycle from all decompiled functions:

```
OnProjectileReachedEnemy  →  GoToPlayer()        (unconditional on hit path)
  → OnProjectileReachedPlayer (this function)
  → success → ResetDeflectStates() → DeflectProjectile(projectile)
      → GoToEnemy()            (unconditional — picks next from availableTargetsLeft)
  → OnProjectileReachedEnemy  (loop)
```

### Remaining uncertainty

A cap could still exist in an undecompiled function:

| Function | VA | Why it might contain the check |
|---|---|---|
| `MoonrangSpecialMove.GetQTEResult` | `0x1805C8990` | Score-keeping, may determine routing |
| `MoonrangSpecialMove.DoMoonrangHitTarget` | `0x1805C87F0` | Final blow — possible cap point |
| `MoonrangProjectile.OnBounce` | `0x1805C4720` | Called every bounce |

However, `DoMoonrangHitTarget` is **not called** from any decompiled success-path function.
If it were the terminator, something in `DeflectProjectile` or `OnProjectileReachedEnemy`
would have to call it — and neither does.

### Implication for the mod patch

With the current patch auto-succeeding every deflect, the Moonerang may bounce forever.
This should be verified in-game: does an unmodded perfect deflect chain eventually end
naturally? If not, a bounce cap may need to be added to the patch.

### What our patch changes (confirmed regardless of termination location)

Without patch: a missed deflect in `OnProjectileReachedPlayer` → `OnDeflectFailed` → chain
ends early. With patch: every deflect succeeds → full bounce chain to whatever the cap is.
The patch cannot cause extra bounces beyond `qteSuccessHitCount` because it does not modify
either counter. Whether the cap check is in `DeflectProjectile` or elsewhere, the patch only
removes the fail-early exit.

### Secondary termination: no targets left

`MoonrangProjectile` maintains `availableTargets` and `availableTargetsLeft`
(`+0x118`/`+0x120`). `RemoveAvailableTarget()` removes dead enemies mid-chain.
`AssignNextEnemyTarget()` picks the next live target; if all targets are dead before the cap
is reached, `DeflectProjectile` would send the projectile toward a null or already-dead
target — this is a pre-existing edge case unrelated to the patch.

---

## Comparison with `TimedBlockHandler.GetResult()`

| | Block QTE | Moonerang Deflect QTE |
|---|---|---|
| State object | `TimedBlockHandler` | `DeflectMoonrangState` |
| "score" method | `TimedBlockHandler.GetResult()` | `DeflectMoonrangState.GetQTEResult()` |
| Result type | `QTEResult` (single) | `TeamQTEResult` (multi-player) |
| "success" flag | `blocking` bool on handler | `TeamQTEResult.HasSuccess` |
| On success action | damage reduction | `DeflectProjectile(projectile)` — next bounce |
| On fail action | full damage | `OnDeflectFailed(projectile)` — chain ends |

The pattern is identical: a state object records whether the player pressed in time, a
`GetQTEResult` method translates that into a `TeamQTEResult`, and the caller branches on
`HasSuccess`.

---

## Patching Strategy

### Option A — Patch `DeflectMoonrangState.GetQTEResult` (recommended)

Mirrors the block-fix approach exactly. Prefix that calls `OnDeflectProjectile()` on the
state, which sets `deflecting = true` before `GetQTEResult` reads it.

> Need Ghidra analysis of `GetQTEResult` (VA `0x180A463B0`) to confirm what field(s) it
> reads before writing the `TeamQTEResult`. Likely reads `deflecting` (0x6C) or
> `deflectEnabled` (0x6D).

### Option B — Prefix on `OnProjectileReachedPlayer`

Force the success path by calling `DeflectProjectile(projectile)` directly and returning
`false` to skip the rest. Problem: the method is `private`, and `DeflectProjectile` is
`protected virtual` — both require `AccessTools` reflection to reach.

### Option C — Patch `DeflectMoonrangState.OnDeflectInput` to fire immediately

`EnableDeflect()` / `OnDeflectInput()` are public. If `OnDeflectInput` is called at the
right moment it sets `deflecting = true` on the state, which `GetQTEResult` will then read
as success. The risk is call-site timing (need to fire it after `EnableDeflect()` but
before `GetQTEResult()` is evaluated).

---

## Relationship to `Soonrang`

`Soonrang : MoonrangSpecialMove` overrides `DeflectProjectile`, `GetPlayerToBounceTo`,
`ResetDeflectStates`, `GetTimeToReachPlayer`, and `GetTimeToReachEnemy`. The
`OnProjectileReachedPlayer` method is NOT overridden, so the same binary executes for both
`MoonrangSpecialMove` (solo Moonerang) and `Soonrang` (team Solstice Strike with Sunboy).
A single patch on the base class covers both.

---

## Key VAs for Future Ghidra Work

| Function | VA |
|---|---|
| `MoonrangSpecialMove.OnProjectileReachedPlayer` | `0x1805C7D80` |
| `DeflectMoonrangState.GetQTEResult` | `0x180A463B0` |
| `DeflectMoonrangState.OnDeflectInput` | `0x180A45EE0` |
| `DeflectMoonrangState.OnDeflectProjectile` | `0x180A465A0` |
| `DeflectMoonrangState.EnableDeflect` | `0x180A45910` |
| `MoonrangSpecialMove.DeflectProjectile` | `0x1805C6EF0` |
| `MoonrangSpecialMove.OnDeflectFailed` | `0x1805C8230` |
| `MoonrangProjectile.OnBounce` | `0x1805C4720` |
