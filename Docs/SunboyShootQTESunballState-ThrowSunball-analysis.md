# `SunboyShootQTESunballState.ThrowSunball` — Analysis

## Summary

`ThrowSunball` starts the throw sequence. It does **not** immediately launch the
projectile — instead it plays an animation and subscribes `OnShootSunball` as a
callback to a Unity animation event. The projectile launches when that animation
event fires. This method writes no fields on `SunboyShootQTESunballState` and
requires no patching from the mod.

---

## Pointer Arithmetic Key

`param_1` = `undefined1 (*)[16]` → each index is a 16-byte block, so `param_1[n]`
= `base + n × 0x10`.

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1[0xc]` | `+0xC0` | `onBeginThrowAnimation` (Action) |
| `*(longlong *)param_1[0x11]` | `+0x110` | `sunboy` (SunboyCombatActor*) |
| `*(param_1[0x11] + 8)` | `+0x118` | `sunballProjectileInstance` (SunballProjectile*) |
| `param_1[7]` | `+0x70` | `stopMaxReachedSFX` (Wwise Event) |
| `sunboy + 0x78` | — | `sunboy.animator` (UnityEngine.Animator) |
| `sunboy + 0x100` | — | `sunboy.animEventHandler` (SunboyAnimationEventHandler) |

---

## Annotated C# Reconstruction

```csharp
public void ThrowSunball()
{
    // ── 1. Fire onBeginThrowAnimation delegate ────────────────────────────────
    // Notifies SolarRainCombatMove (or other subscribers) that the throw has begun.
    onBeginThrowAnimation?.Invoke();   // +0xC0

    // ── 2. Play throw animation on sunboy ─────────────────────────────────────
    Animator anim = sunboy.animator;   // sunboy+0x78
    if (anim != null)
        anim.Play(SunboyAnims.shootSunballHash, 0);

    // ── 3. Stop the "max charge reached" looping SFX ─────────────────────────
    SunballProjectile proj = sunballProjectileInstance;   // +0x118
    if (proj != null)
        stopMaxReachedSFX.Post(proj.gameObject);   // +0x70

    // ── 4. Subscribe OnShootSunball to the animation event ───────────────────
    // The actual projectile launch happens later, when the throw animation
    // reaches the "release" animation event keyframe.
    SunboyAnimationEventHandler handler =
        sunboy.animEventHandler as SunboyAnimationEventHandler;   // sunboy+0x100

    Action callback = new Action(this.OnShootSunball);
    handler.add_onShootSunball(callback);   // one-shot subscription
}
```

---

## Execution Chain

```
ThrowSunball()  ← called by BeginShoot (when throwSunballWhenReady)
                           or by SolarRainCombatMove.OnSunballReady + garlInPosition
    │
    ├─ onBeginThrowAnimation.Invoke()           → SolarRainCombatMove (animation start)
    ├─ animator.Play(shootSunballHash)           → Unity plays the throw animation
    ├─ stopMaxReachedSFX.Post(proj.gameObject)   → Wwise stops charge loop SFX
    └─ animEventHandler.add_onShootSunball(OnShootSunball)
                                                 → registered; waits for anim keyframe
           │
           └─ [Unity animation event fires mid-throw]
                     │
                     └─ SunboyAnimationEventHandler.OnShootSunball()  (VA 0x180A23650)
                               │
                               └─ SunboyShootQTESunballState.OnShootSunball()
                                        └─ projectile launched, throwSFX posted,
                                           onThrowSunball delegate fired
```

The throw SFX (`+0x80`) is not posted here — almost certainly posted inside
`OnShootSunball` at the moment the projectile actually leaves Sunboy's hand.

---

## `SunboyAnimationEventHandler` (Confirmed)

```csharp
public class SunboyAnimationEventHandler : PlayerAnimationEventHandler
{
    private Action onShootSunball;  // 0x70

    public void add_onShootSunball(Action value) { }     // VA: 0x180B2A0C0
    public void remove_onShootSunball(Action value) { }  // VA: 0x180B2A1B0
    public void OnShootSunball() { }  // VA: 0x180A23650
                                      // ↑ Unity calls this by name from animation event
}
```

Unity invokes `SunboyAnimationEventHandler.OnShootSunball()` by name from a
Mecanim animation event embedded in the throw clip. That method then invokes its
`onShootSunball` Action, which calls back to
`SunboyShootQTESunballState.OnShootSunball()`.

---

## Mod Relevance — Auto-Release

None. `ThrowSunball` and everything downstream fire naturally as part of
`BeginShoot`'s success path. The three-write patch is the complete mod:

```csharp
*(int*)(b + 0xE0)    = 0;    // FailDidNoPress(4) → SuccessPerfect(0)
*(bool*)(b + 0x134)  = true; // playerInputDone
*(float*)(b + 0x128) = -1f;  // expire step timer → BeginShoot next frame
```

---

## Full Automation — Sequence Once AutoRelease Fires

```
AutoRelease() writes 3 fields
    └─ next UpdateCharging:
         Phase 3: timer expired, playerInputDone → BeginShoot()
              ├─ AddResult(playerQTEResult { result=SuccessPerfect, owner=player })
              ├─ proj.level == maxLevel → success
              │    ├─ DoQTESuccessFeedback()
              │    ├─ onSunballReady.Invoke() → SolarRainCombatMove.OnSunballReady
              │    ├─ currentStep = 2
              │    └─ throwSunballWhenReady OR SolarRainCombatMove triggers ThrowSunball()
              └─ ThrowSunball() → anim → animation event → OnShootSunball() → projectile
```

The mod, as designed with three writes, is **complete for auto-release**.

---

## Full Automation — Remaining Unknown: Charge Auto-Start

The uncharted region is between `StateEnter` → `BeginDisplayInstructions` and the
charge loop actually starting. Based on the method list:

| Method | VA | Likely role |
|---|---|---|
| `BeginIn` | `0x180B258F0` | Sets up the "waiting for press A" phase |
| `UpdateIn` | `0x180B27C20` | Per-frame: polls for press A, calls `BeginCharge` |
| `BeginCharge` | `0x180B25A80` | Spawns sunball, starts charge step timer |

**`UpdateIn` is the gate.** Decompile it to confirm the `GetButtonDown("Attack")`
call and identify the cleanest bypass (Prefix suppressing the press check and
calling `BeginCharge` directly, or a Postfix on `BeginDisplayInstructions`
immediately calling `BeginCharge`).

Suppressing `BeginDisplayInstructions` (Prefix returning false) is safe and
confirmed — it writes nothing.
