# `MoonrangSpecialMove$$DeflectProjectile` — Ghidra Analysis

**VA**: `0x1805C6EF0`  
**RVA**: `0x5C6EF0`  
**Class**: `MoonrangSpecialMove`  
**dump.cs signature**: `protected virtual void DeflectProjectile(MoonrangProjectile projectile)` — Slot 62  
**Overridden in**: `Soonrang` (team Solstice Strike variant)

---

## Parameter Mapping

| Ghidra param | C# identity |
|---|---|
| `param_1` | `MoonrangSpecialMove* this` |
| `param_2` | `MoonrangProjectile* projectile` |
| `param_3` / `param_4` | IL2CPP hidden args |

---

## Field Offset Cross-Reference

### `MoonrangSpecialMove` (`param_1`)

Each index step = 8 bytes.

| Ghidra expression | Byte offset | dump.cs field |
|---|---|---|
| `param_1[0x14]` | +0xA0 | `deflectFX` (`GameObject`) |
| `param_1[0x16]` | +0xB0 | `deflectSFX` (`Event`, Wwise) |
| `*(param_1 + 0x2b)` | +0x158 | `waitAndGoIdleCoroutine` (`Coroutine`) |

### `MoonrangProjectile` (`param_2`)

| Ghidra expression | Byte offset | dump.cs field |
|---|---|---|
| `param_2[0x1b]` | +0xD8 | `currentTarget` (`CombatTarget`) — **written** |
| `param_2[0x1d]` | +0xE8 | `currentPlayerTarget` (`CombatActor`) — read |
| `*(int *)(param_2 + 0x1e)` | +0xF0 | `bounceCount` (`int`) — **incremented** |
| `param_2[0x24]` | +0x120 | `availableTargetsLeft` (`List<CombatTarget>`) |

### `CombatActor` (via `currentPlayerTarget`, local `ppppplVar3`)

| Ghidra expression | Byte offset | dump.cs field |
|---|---|---|
| `ppppplVar3[0xf]` | +0x78 | `dependencies` (`CombatActorDependencies`) |

### `CombatActorDependencies` (via `dependencies`, local `pppppplVar13`)

| Ghidra expression | Byte offset | dump.cs field |
|---|---|---|
| `pppppplVar13[7]` | +0x38 | `lookDirectionController` (`LookDirectionController`) |

---

## Vtable Calls

| Ghidra index | Inferred method | Basis |
|---|---|---|
| `(*param_1)[0xa5]` | `GetTimeToReachEnemy(MoonrangProjectile, Vector3) : float` | signature: takes projectile + Vector3 ref, returns float (uVar18 used as duration in GoToEnemy) |

---

## Step-by-Step Execution

### 0. Lazy static init
One-time registration of `Method$Manager<FXManager>.get_Instance()`. Standard IL2CPP guard.

---

### 1. Get FXManager instance
```csharp
FXManager fxm = FXManager.Instance;  // FUN_180012b10(...)
```

---

### 2. Get projectile world position
```csharp
Transform t = projectile.GetComponent<Transform>();     // UnityEngine.Component::get_transform()
Vector3 projPos = t.position;                           // get_position_Injected
```

---

### 3. Play deflect FX
```csharp
fxm.PlayFX(this.deflectFX, projPos, ...);               // deflectFX = MoonrangSpecialMove+0xA0
```
FX is played at the projectile's current world position.

---

### 4. Play deflect SFX (Wwise)
```csharp
this.deflectSFX.Post(projectile.GetComponent<GameObject>());   // deflectSFX = +0xB0
```

---

### 5. Static init for List methods
One-time registration of `List<CombatTarget>.RemoveAt`, `.get_Count`, `.get_Item`. Standard guard.

---

### 6. Pick the next enemy target from `availableTargetsLeft`
```csharp
List<CombatTarget> targetsLeft = projectile.availableTargetsLeft;   // +0x120

// Refill from full target list if exhausted
if (targetsLeft.Count < 1)
    MoonrangProjectile.RefillAvailableTargetsLeft(projectile);

// Always take the LAST item (LIFO / stack-like pop)
int lastIdx = targetsLeft.Count - 1;
CombatTarget nextTarget = targetsLeft[lastIdx];
targetsLeft.RemoveAt(lastIdx);

// Assign as the projectile's next target
projectile.currentTarget = nextTarget;             // +0xD8
```

The list is refilled automatically when empty, so targets cycle through indefinitely.

---

### 7. Face the player toward the new target
```csharp
CombatActor currentPlayer = projectile.currentPlayerTarget;          // +0xE8
CombatActorDependencies deps = currentPlayer.dependencies;           // CombatActor+0x78
LookDirectionController lookDir = deps.lookDirectionController;      // CombatActorDependencies+0x38

Transform targetLookPoint = nextTarget.someTransform;                // nextTarget[0xd] = +0x68
LookDirectionController.LookAt(lookDir, targetLookPoint.position);
```

---

### 8. Increment `bounceCount` on the projectile
```csharp
projectile.bounceCount++;       // MoonrangProjectile+0xF0
```
Note: this is **separate** from `MoonrangSpecialMove.hitCount` (+0x140), which is incremented in
`OnProjectileReachedEnemy`. `bounceCount` lives on the projectile instance; `hitCount` lives on the move.

---

### 9. Notify the projectile it is bouncing
```csharp
MoonrangProjectile.OnBounce(projectile, 0);
```

---

### 10. Compute impact position on the new target
```csharp
Vector3 impactPos = CombatTarget.GetProjectileImpactPosition(nextTarget, projectile);
```

---

### 11. Compute travel duration
```csharp
float duration = GetTimeToReachEnemy(projectile, impactPos);     // vtable[0xa5]
```
`GetTimeToReachEnemy` is `protected virtual` (Slot 63 in dump.cs), overridden in `Soonrang`.

---

### 12. Send the projectile to the next enemy
```csharp
MoonrangProjectile.GoToEnemy(projectile, nextTarget, impactPos, duration);
```

---

### 13. Cancel any pending idle timeout
```csharp
if (this.waitAndGoIdleCoroutine != null)           // MoonrangSpecialMove+0x158
{
    this.StopCoroutine(this.waitAndGoIdleCoroutine);
    this.waitAndGoIdleCoroutine = null;
}
return;
```
`waitAndGoIdleCoroutine` is a fallback timeout that would return Moongirl to idle if a bounce
stalled. A successful deflect cancels it because the projectile is now in flight again.

---

## Reconstructed C#

```csharp
// MoonrangSpecialMove — VA 0x1805C6EF0
protected virtual void DeflectProjectile(MoonrangProjectile projectile)
{
    // 1-2. FX and SFX at projectile position
    FXManager.Instance.PlayFX(this.deflectFX, projectile.transform.position);
    this.deflectSFX.Post(projectile.gameObject);

    // 3. Pick next enemy target (LIFO from availableTargetsLeft, auto-refilled when empty)
    if (projectile.availableTargetsLeft.Count < 1)
        projectile.RefillAvailableTargetsLeft();

    int lastIdx = projectile.availableTargetsLeft.Count - 1;
    CombatTarget nextTarget = projectile.availableTargetsLeft[lastIdx];
    projectile.availableTargetsLeft.RemoveAt(lastIdx);
    projectile.currentTarget = nextTarget;

    // 4. Face player toward the new target
    LookDirectionController ldc =
        projectile.currentPlayerTarget.dependencies.lookDirectionController;
    ldc.LookAt(nextTarget.lookAtPoint.position);

    // 5. Increment bounce counter on the projectile
    projectile.bounceCount++;

    // 6. Notify projectile of bounce event
    projectile.OnBounce();

    // 7. Compute arc and launch
    Vector3 impactPos = nextTarget.GetProjectileImpactPosition(projectile);
    float duration    = GetTimeToReachEnemy(projectile, impactPos);   // virtual
    projectile.GoToEnemy(nextTarget, impactPos, duration);

    // 8. Cancel idle-timeout coroutine — projectile is in flight again
    if (this.waitAndGoIdleCoroutine != null)
    {
        this.StopCoroutine(this.waitAndGoIdleCoroutine);
        this.waitAndGoIdleCoroutine = null;
    }
}
```

---

## Critical Finding: No Termination Check

**There is no `hitCount >= qteSuccessHitCount` comparison anywhere in this function.**

`GoToEnemy` is called unconditionally once a valid target exists. Combined with the confirmed
absence of any such check in `OnProjectileReachedEnemy` (see
[OnProjectileReachedEnemy-analysis.md](OnProjectileReachedEnemy-analysis.md)), the evidence
now strongly supports **Option 1**:

> **The Moonerang bounce chain loops indefinitely until the player misses a deflect.**

`qteSuccessHitCount` and `hitCount` exist on `MoonrangSpecialMove` and are tracked, but based
on all decompiled functions so far, no code branches on them to end the bounce loop.

### Remaining uncertainty

The check could still exist in an undecompiled function. Remaining candidates (all undecompiled):

| Function | VA | Why it might contain the check |
|---|---|---|
| `MoonrangSpecialMove.GetQTEResult` | `0x1805C8990` | Score-keeping, may determine final-hit routing |
| `MoonrangSpecialMove.DoMoonrangHitTarget` | `0x1805C87F0` | Final blow method — may be the cap enforcement point |
| `MoonrangProjectile.OnBounce` | `0x1805C4720` | Called on every bounce, could gate behaviour |

However, `DoMoonrangHitTarget` is **not called** from any decompiled function in the success
path — it is only referenced by name in dump.cs. If it were the cap enforcer, something
would have to call it from within `DeflectProjectile` or `OnProjectileReachedEnemy`, and
neither do. This further supports Option 1.

### `availableTargetsLeft` cycling

`RefillAvailableTargetsLeft` is called when `availableTargetsLeft.Count < 1`. This means
when all enemies have been hit once, the list is replenished and the projectile cycles back
to the beginning — there is no "ran out of targets" natural termination either.

---

## Relationship to the Mod Patch

Our patch guarantees every deflect in `DeflectMoonrangState.GetQTEResult` scores as
`SuccessBeforeEvent`, meaning `OnProjectileReachedPlayer` will always reach the success
branch and call `DeflectProjectile`. With the finding above:

- **If Option 1 (loops until failure) is confirmed**: the patch will cause the Moonerang to
  bounce forever with no natural endpoint. This is a meaningful behaviour change that may
  need a cap — either by capping based on `qteSuccessHitCount` in the patch itself, or by
  intentionally accepting infinite bouncing.
- **If a cap exists elsewhere**: the patch is safe as-is and simply ensures the cap is
  always reached rather than failing out early.

This should be verified in-game by observing whether a normal (unmodded) perfect chain of
deflects eventually ends on its own.

---

## Related Analysis Files

| File | Function |
|---|---|
| [OnProjectileReachedPlayer-Moonerang-analysis.md](OnProjectileReachedPlayer-Moonerang-analysis.md) | Player-side QTE gate — calls DeflectProjectile on success |
| [OnProjectileReachedEnemy-analysis.md](OnProjectileReachedEnemy-analysis.md) | Enemy-side handler — increments hitCount, unconditionally calls GoToPlayer |
