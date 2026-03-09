# `SunboyShootQTESunballState.OnWentPastMaxCharge` — Analysis

## Summary

**This method only posts a Wwise stop-SFX event. It performs zero field writes.**
It does not set `shootOnNextChargeStep`, does not drop the level, does not reset the
timer. All of that logic is inline in `UpdateCharging`.

---

## Pointer Arithmetic Key

`param_1` = `this` (longlong base address, byte-addressed)

| Expression | Byte offset | C# field |
|---|---|---|
| `*(longlong *)(param_1 + 0x118)` | `+0x118` | `sunballProjectileInstance` (SunballProjectile*) |
| `*(longlong **)(param_1 + 0x70)` | `+0x70` | `stopMaxReachedSFX` (Wwise Event) |

---

## Annotated C# Reconstruction

```csharp
private void OnWentPastMaxCharge()
{
    SunballProjectile proj = sunballProjectileInstance;   // +0x118
    Event stopSfx = stopMaxReachedSFX;                    // +0x70

    // Posts "stop the charging loop SFX" on the projectile's GameObject.
    if (proj != null && stopSfx != null)
    {
        stopSfx.Post(proj.gameObject);   // AK.Wwise.Event.Post(gameObject)
        return;
    }

    FatalError();   // either pointer was null — should not happen in normal play
}
```

The Wwise `Event.Post(GameObject)` call uses the projectile's GameObject as the
emitter so the audio engine knows where in 3D space to stop the sound.

---

## What this method is NOT

Previous analysis speculated that `OnWentPastMaxCharge` might contain the full
"level drop" logic. It does not. The following all happen **inline in
`UpdateCharging`**, not here:

- `shootOnNextChargeStep = true`   (`+0x124`)
- `proj.SetLevel(proj.level - 1)`  (4 → 3)
- timer reset: `currentChargeStepDuration = chargeStepDuration + currentChargeStepDuration`

`OnWentPastMaxCharge` is called from within that inline block, purely for audio.

Similarly, `OnReachedMaxCharge` (VA `0x180B27AE0`) is presumably the mirror —
posts `playMaxReachedSFX` when level first hits max. Neither method gates logic.

---

## Mod Implication: Patch Strategy Correction

The earlier suggestion — "Prefix on `OnWentPastMaxCharge` returning `false` to
suppress header drop" — **would not work**. Suppressing this method silences the
SFX but leaves all the level-drop logic in `UpdateCharging` running unchanged.

The correct interception remains:

```csharp
// Prefix on UpdateCharging (or StateExecute), fires every frame.
// When condition is met, write three fields and return — the game does the rest.
unsafe void AutoRelease(SunboyShootQTESunballState instance)
{
    byte* b = (byte*)instance.Pointer;
    *(int*)(b + 0xE0)    = 0;     // result: FailDidNoPress(4) → SuccessPerfect(0)
    *(bool*)(b + 0x134)  = true;  // playerInputDone — gates Phase 4 input check
    *(float*)(b + 0x128) = -1f;   // currentChargeStepDuration — expire step timer
}
```

Condition to check before writing:
```csharp
int   level    = *(int*)(projPtr + 0xCC);
int   maxLevel = *(int*)(b + 0xB0);
bool  done     = *(bool*)(b + 0x134);
bool  falling  = *(bool*)(b + 0x124);
return level == maxLevel && !done && !falling;
```

---

## `OnReachedMaxCharge` — Predicted Behaviour (not yet decompiled)

By symmetry, expected to be:
```csharp
private void OnReachedMaxCharge()
{
    playMaxReachedSFX.Post(sunballProjectileInstance.gameObject);   // +0x68
}
```
No field writes. Purely audio. Same conclusion: not a useful hook for the mod.
