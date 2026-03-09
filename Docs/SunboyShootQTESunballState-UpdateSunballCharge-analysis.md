# `SunboyShootQTESunballState.UpdateSunballCharge` — Analysis

## Overview

`UpdateSunballCharge` is a private per-frame method on `SunboyShootQTESunballState`
called from `UpdateCharging` / `StateExecute` while the Soonrang sunball QTE charge
step is active. Its sole job is to keep two external systems in sync with the
current internal charge value:

1. **Wwise audio** — pushes `sunballCharge × 100` to the `sunballChargeLevelRTPC`
   RTPC parameter so the charge SFX pitch/volume can respond in real time.
2. **`SunballProjectile` visual** — calls `SetCharge(sunballCharge)` on the live
   projectile instance so its animator, light intensity, and shake all reflect the
   current charge level.

---

## Field Map (`param_1` = `this : SunboyShootQTESunballState`)

| Offset | C# field | Type | Role in function |
|--------|----------|------|-----------------|
| `+0x60` | `sunballChargeLevelRTPC` | `RTPC` | Target of the Wwise RTPC update |
| `+0xD0` | `sunballCharge` | `float` | Current charge value `[0, 1]` |
| `+0x118` | `sunballProjectileInstance` | `SunballProjectile` | Live projectile to update |

---

## Annotated Pseudocode

```c
void UpdateSunballCharge(SunboyShootQTESunballState* self, ...)
{
    // Load the two fields we'll need.
    SunballProjectile* projectile = self->sunballProjectileInstance;   // +0x118
    RTPC*             rtpc        = self->sunballChargeLevelRTPC;      // +0x60

    if (projectile == null)
        goto NullReferenceException;   // IL2CPP null-check bailout

    // ── IL2CPP method-pointer cache lookup ──────────────────────────────────
    // DAT_183a1fbb8 is a static slot that caches the resolved pointer for
    // UnityEngine.Component::get_gameObject(). On first call it calls
    // FUN_18029a8a0 (il2cpp_resolve_icall) to look it up; on every subsequent
    // call the cached pointer is used directly.
    GameObject* go = Component_get_gameObject(projectile);

    // ── Wwise RTPC update ───────────────────────────────────────────────────
    // Only sent when an RTPC object is configured (rtpc != null).
    // The charge is stored as [0, 1] internally but Wwise expects [0, 100].
    if (rtpc != null)
    {
        AK.Wwise.RTPC.SetValue(rtpc, go, self->sunballCharge * 100.0f, null);

        // Reload in case GC or engine internals moved the pointer
        // (defensive re-read of the same field).
        projectile = self->sunballProjectileInstance;   // +0x118

        if (projectile != null)
        {
            // ── SunballProjectile visual update ──────────────────────────────
            // SetCharge(float) drives:
            //   • animator blend tree  (charge anim)
            //   • point-light intensity lerp
            //   • camera shake strength
            //   • chargeRatio (+0xFC) read by other systems
            projectile->SetCharge(self->sunballCharge);
            return;
        }
    }

NullReferenceException:
    // FUN_1802845b0 = IL2CPP null-reference exception factory.
    // swi(3) = ARM64 supervisor call used by IL2CPP as an unconditional trap
    // after the exception is dispatched; execution never continues past here.
    throw NullReferenceException(...);
}
```

---

## Key Details

### IL2CPP method-pointer cache (`DAT_183a1fbb8`)

IL2CPP resolves managed method pointers lazily at first call and stores them in
static data slots (`.data` segment, one slot per call site). The pattern is:

```c
if (DAT_183a1fbb8 == null)
    DAT_183a1fbb8 = il2cpp_resolve_icall("UnityEngine.Component::get_gameObject()");
result = DAT_183a1fbb8(this);
```

This is not a virtual dispatch — it is a direct C function pointer call after
a one-time lookup.

### RTPC scale: `sunballCharge × 100`

`sunballCharge` (`+0xD0`) is maintained in the normalised `[0, 1]` range by
`UpdateCharging`. Wwise RTPC parameters are conventionally authored in the
`[0, 100]` range for human-readable mixer automation, so the `× 100` conversion
here is the only place the domain change happens.

### Double null-check on `sunballProjectileInstance`

The field is read twice:
1. At function entry — guards against calling `get_gameObject()` on a null
   projectile.
2. After `RTPC.SetValue` — defensive re-read before calling `SetCharge`.

The second reload is unusual but consistent with defensive IL2CPP codegen
guarding against any re-entrant callback inside `SetValue` that could
theoretically nullify the projectile (e.g. a state-machine exit triggered
by an audio event). In practice it is unlikely to differ from the first read.

### Early-out when `rtpc == null`

If no RTPC asset is assigned in the inspector, the Wwise call is skipped **and
so is `SetCharge`**. This means the projectile visual won't update either.
Whether that is intentional (the two updates are considered inseparable) or an
accidental coupling through the single `if (rtpc != null)` guard is unclear
from the binary alone; the C# source presumably had them in sequence with the
RTPC call gated and `SetCharge` unconditional.

---

## C# Reconstruction

```csharp
private void UpdateSunballCharge()
{
    if (sunballProjectileInstance == null)
        return;

    GameObject go = sunballProjectileInstance.gameObject;

    if (sunballChargeLevelRTPC != null)
        sunballChargeLevelRTPC.SetValue(go, sunballCharge * 100f);

    if (sunballProjectileInstance != null)
        sunballProjectileInstance.SetCharge(sunballCharge);
}
```

> **Note:** The compiled form gates `SetCharge` inside the `rtpc != null` block,
> making both calls conditional on the RTPC being set. The reconstruction above
> reflects the likely *intended* C# (both calls independent), while the comment
> documents the discrepancy.

---

## Summary

| What | Method / API |
|------|-------------|
| Charge field read | `SunboyShootQTESunballState.sunballCharge` (`+0xD0`) |
| Audio update | `AK.Wwise.RTPC.SetValue(rtpc, gameObject, charge × 100, null)` |
| Visual update | `SunballProjectile.SetCharge(charge)` |
| Null-guard subject | `sunballProjectileInstance` (`+0x118`) — checked twice |
| Exception path | IL2CPP null-ref factory + `swi(3)` trap |
