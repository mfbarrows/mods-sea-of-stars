# `SunballProjectile.SetCharge` — Analysis

## Overview

`SetCharge(float charge)` is called every frame during the Soonrang charge QTE
(from `SunboyShootQTESunballState.UpdateSunballCharge`) and locally from
`SunballProjectile.UpdateCharge`. It is the single write path for three
independently-shaped output fields that downstream systems poll on their own
update cycles:

| Output field | Downstream consumer | Shape |
|---|---|---|
| `currentCharge` (`+0xF8`) | `get_CurrentCharge()` property, external callers | Raw (unbounded) |
| `chargeRatio` (`+0xFC`) | `get_ChargeRatio()` property, animator blend parameter | Linear, upper-clamped to 1 |
| `lightTargetIntensity` (`+0x13C`) | `UpdateLightIntensityInterpolation()` | Ease-in quadratic, lerped over intensity range |
| `pullBack` (`+0x134`) | `Shake()` / position offset in `Update()` | Linear, clamped `[0, 1]` × 0.2 units |

---

## Field Map (`param_1` = `this : SunballProjectile`)

| Offset | C# field | Role |
|--------|----------|------|
| `+0xDC` | `currentLightMinIntensity` | Lower bound of the light lerp range (set at init/level-up) |
| `+0xE0` | `currentLightMaxIntensity` | Upper bound of the light lerp range |
| `+0xF8` | `currentCharge` | Raw charge as supplied by caller |
| `+0xFC` | `chargeRatio` | Normalised `[0, 1]` charge read by animator |
| `+0x134` | `pullBack` | Physical displacement of projectile toward player |
| `+0x13C` | `lightTargetIntensity` | Target intensity fed to `UpdateLightIntensityInterpolation` |

---

## Annotated Pseudocode

```c
void SetCharge(SunballProjectile* self, float charge)
{
    // ── IL2CPP class initializer guard (TweenFunctions.Quadratic) ───────────
    // DAT_183a0fd4e is a compile-unit bool cached from the first call.
    // FUN_1802a4cc0 = il2cpp_runtime_class_init.
    // The inner check on TypeInfo[0x1C] is the authoritative initialized flag
    // inside the type-metadata struct — a standard two-phase IL2CPP guard.
    if (!DAT_183a0fd4e) {
        il2cpp_runtime_class_init(&TweenFunctions.Quadratic_TypeInfo);
        DAT_183a0fd4e = true;
    }
    if (!TweenFunctions.Quadratic_TypeInfo.initialized)   // TypeInfo[0x1C]
        RunClassConstructor(TweenFunctions.Quadratic_TypeInfo);

    // ── Save raw (unbounded) charge ──────────────────────────────────────────
    float lightMin = self->currentLightMinIntensity;   // +0xDC
    float lightMax = self->currentLightMaxIntensity;   // +0xE0
    self->currentCharge = charge;                      // +0xF8  raw, written first

    // ── Clamp to [0, 1] for the eased outputs ───────────────────────────────
    float clamped = Math.Clamp(charge, 0f, 1f);        // fVar3

    // ── Quadratic ease-in (TweenFunctions.Quadratic inlined as x²) ──────────
    // The class initializer above populates TweenFunctions.Quadratic.InFunc but
    // the compiler has inlined the simple x² math rather than call through it.
    float eased = clamped * clamped;
    eased = Math.Clamp(eased, 0f, 1f);                 // defensive post-clamp

    // ── Reload currentCharge for the linear outputs ──────────────────────────
    float raw = self->currentCharge;                   // re-read +0xF8

    // ── Output 1: light target intensity ─────────────────────────────────────
    // Lerp over the inspector-configured light range using the EASED charge.
    // UpdateLightIntensityInterpolation() smoothly moves pointLight.intensity
    // toward this target each frame.
    self->lightTargetIntensity = (lightMax - lightMin) * eased + lightMin;  // +0x13C

    // ── Output 2: pull-back displacement ─────────────────────────────────────
    // Physical offset of the projectile toward Sunboy during charging.
    // Uses the RAW charge (clamped to [0,1]), giving a LINEAR 0 → 0.2 units.
    float rawClamped = Math.Clamp(raw, 0f, 1f);
    self->pullBack = rawClamped * 0.2f;                 // +0x134

    // ── Output 3: chargeRatio (animator blend parameter) ─────────────────────
    // ONLY upper-clamped (min(charge, 1.0) = fVar5).
    // The lower-clamp (max(0, ...)) is applied to fVar3 but NOT carried into
    // chargeRatio. See "Quirk" note below.
    self->chargeRatio = Math.Min(charge, 1f);           // +0xFC
}
```

---

## Three Distinct Output Shapes

### 1. `lightTargetIntensity` — ease-in quadratic

$$I_\text{target} = (I_\text{max} - I_\text{min}) \cdot \text{clamp}(t,0,1)^2 + I_\text{min}$$

The squaring makes the light barely grow at low charge and punch dramatically
bright near full charge. `UpdateLightIntensityInterpolation` then lerps
`pointLight.intensity` toward this target over time, adding a further smooth lag.

### 2. `pullBack` — linear

$$d_\text{pullback} = \text{clamp}(t, 0, 1) \times 0.2$$

The projectile is displaced up to 0.2 world units back toward Sunboy's hand. The
linear shape gives a constant apparent size change, which reads more clearly as
a hold/tension gesture than an eased one would.

### 3. `chargeRatio` — upper-clamped linear (quirk below)

$$r = \min(t, 1)$$

Used as the animator's blend parameter. Linear 0 → 1, no easing. The animator
blend tree presumably handles its own interpolation internally.

---

## Quirk: `chargeRatio` is only upper-clamped

`fVar5 = min(charge, 1.0)` — the upper clamp is applied before the double-clamp
loop that produces `fVar3`, but `chargeRatio` is written from `fVar5`, not
`fVar3`. This means:

- If `charge < 0` is passed, `chargeRatio` goes negative.
- `lightTargetIntensity` and `pullBack` are both computed from the fully-clamped
  value and would stay at their floor values.

In practice `charge` starts at `0` and increments monotonically (from
`AddInputCharge` and `UpdateCharge`), so this path is never taken. It is most
likely a compiler artefact from how the IL was ordered rather than an intentional
distinction.

---

## Why `currentCharge` is re-read after write

```c
self->currentCharge = charge;      // write
...
float raw = self->currentCharge;   // re-read
```

The re-read is standard IL2CPP defensive codegen: the compiler does not alias
the write back through a local, so it reloads the field. In a managed context a
re-entrant callback inside `RTPC.SetValue` (called by `UpdateSunballCharge`
before this) could have modified the field; here no such callback exists, so
`raw == charge` always.

---

## Role of `TweenFunctions.Quadratic` class init

The static class constructor for `TweenFunctions.Quadratic` populates four
`Func<>` delegates (`InFunc`, `OutFunc`, etc.). The init guard runs here because
the compiler saw a reference to the type, but `SetCharge` never actually calls
through those delegates — the `x²` math is fully inlined. The guard is therefore
present for correctness (another code path that *does* use `InFunc` may run
from an inlined call site placed here by the JIT), not because this function
needs the delegates itself.

---

## C# Reconstruction

```csharp
public void SetCharge(float charge)
{
    float lightMin = currentLightMinIntensity;
    float lightMax = currentLightMaxIntensity;
    currentCharge = charge;

    // Quadratic ease-in on clamped charge → drives light
    float clamped = Mathf.Clamp01(charge);
    float eased   = Mathf.Clamp01(clamped * clamped);
    lightTargetIntensity = Mathf.Lerp(lightMin, lightMax, eased);

    // Linear clamped charge → drives physical pull-back
    pullBack = Mathf.Clamp01(currentCharge) * 0.2f;

    // Upper-clamped charge → drives animator blend (lower clamp omitted in binary)
    chargeRatio = Mathf.Min(charge, 1f);
}
```
