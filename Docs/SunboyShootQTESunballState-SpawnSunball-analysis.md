# `SunboyShootQTESunballState.SpawnSunball` — Analysis

## Summary

`SpawnSunball` instantiates the sunball projectile from a pool, positions it at
Sunboy's hand, initialises it, and starts its audio. It writes exactly **one
state field**: `sunballProjectileInstance` (+0x118). All other operations are
on external objects (pool, projectile, audio engine).

**This method is safe to invoke directly via `AccessTools.Method` in the
auto-start patch.** All fields it reads from `this` are set by `StateEnter`
and never change.

---

## Pointer Arithmetic Key

`param_1` is `longlong *******`, so `param_1[n]` = `*(base + n×8)` and
`param_1 + n` as an address = `base + n×8`.

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1[0x11]` | `+0x88` | `sunballProjectilePrefab` (GameObject) — pool lookup key |
| `param_1 + 0x23` | `+0x118` | `sunballProjectileInstance` (SunballProjectile*) — **written here** |
| `param_1[0x22]` | `+0x110` | `sunboy` (SunboyCombatActor*) |
| `param_1[0x15]` | `+0xA8` | `target` (CombatTarget) |
| `param_1[0xf]` | `+0x78` | `chargeSFX` (Wwise Event) |
| `param_1[0xc]` | `+0x60` | `sunballChargeLevelRTPC` (Wwise RTPC) |

---

## Annotated C# Reconstruction

```csharp
private void SpawnSunball()
{
    // ── 1. Get a pooled instance of the sunball prefab ────────────────────────
    PoolManager pool = Manager<PoolManager>.get_Instance();
    GameObject pooledGO = pool.GetObjectInstance(sunballProjectilePrefab);   // +0x88
    if (pooledGO == null) { FatalError(); return; }

    // ── 2. Get the SunballProjectile component and cache it ───────────────────
    SunballProjectile proj = pooledGO.GetComponent<SunballProjectile>();
    sunballProjectileInstance = proj;   // +0x118 ← THE ONLY STATE WRITE

    // ── 3. Position the projectile at Sunboy's hand ───────────────────────────
    Vector3 spawnPos = sunboy.GetSunballSpawnPosition();     // +0x110
    proj.transform.position = spawnPos;

    // ── 4. Initialise the projectile ──────────────────────────────────────────
    // Called via virtual dispatch (vtable slot 0x41/0x42).
    // Arguments passed: sunboy (+0x110) and target (+0xA8).
    // Almost certainly SunballProjectile.Init(SunboyCombatActor, CombatTarget)
    // or similar — sets up damage, owner reference, etc.
    proj.Init(sunboy, target);   // virtual call

    // ── 5. Start charge SFX ──────────────────────────────────────────────────
    chargeSFX.Post(proj.gameObject);   // +0x78

    // ── 6. Set charge level RTPC to zero ─────────────────────────────────────
    sunballChargeLevelRTPC.SetValue(proj.gameObject);   // +0x60
    // (initial charge = 0; RTPC will be updated per-frame in UpdateSunballCharge)
}
```

---

## Fields Read (all set before SpawnSunball is called)

| Field | Offset | Set by |
|---|---|---|
| `sunballProjectilePrefab` | `+0x88` | Unity inspector / serialized config |
| `sunboy` | `+0x110` | `StateEnter` via GetComponent |
| `target` | `+0xA8` | Set externally before StateEnter (by the move that owns this state) |
| `chargeSFX` | `+0x78` | Unity inspector |
| `sunballChargeLevelRTPC` | `+0x60` | Unity inspector |

None of these depend on the `UpdateIn` animation phase. They are all valid the
moment `StateEnter` returns. **SpawnSunball can be called at any point after
StateEnter without risk.**

---

## Field Written

| Field | Offset | Effect |
|---|---|---|
| `sunballProjectileInstance` | `+0x118` | Non-null after return; allows UpdateCharging to read `proj.level`, call `proj.SetLevel()`, etc. |

If `+0x118` is null when `UpdateCharging` runs, every read from it hits a
null-check and `FatalError`. This write is therefore the **sole prerequisite**
the auto-start patch must ensure before `UpdateCharging` runs.

---

## Mod Implication — Auto-Start Patch Is Confirmed Safe

The revised and confirmed auto-start Prefix:

```csharp
[HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateIn")]
static class Patch_UpdateIn_AutoStart
{
    static bool Prefix(SunboyShootQTESunballState __instance)
    {
        unsafe
        {
            byte* b = (byte*)__instance.Pointer;
            if (*(int*)(b + 0x120) != 0) return true;   // not In step — run original

            // 1. Spawn the projectile (sets sunballProjectileInstance at +0x118)
            AccessTools.Method(typeof(SunboyShootQTESunballState), "SpawnSunball")
                       .Invoke(__instance, null);

            // 2. Advance to Charging step
            *(int*)(b + 0x120) = 1;                     // currentStep = Charging

            // 3. Load first charge step timer
            *(float*)(b + 0x128) = *(float*)(b + 0x58); // currentChargeStepDuration = chargeStepDuration

            return false;   // suppress UpdateIn
        }
    }
}
```

**Order matters**: SpawnSunball first, then set `currentStep = 1`. If
`currentStep` is written to 1 before the projectile is spawned and the original
`UpdateIn` somehow fires again (it won't with `return false`, but defensively),
`UpdateCharging` would null-deref. As written, this cannot happen.

---

## Full Confirmed Field Write Summary for Auto-Start

After the Prefix fires:

| Field | Offset | Value | Written by |
|---|---|---|---|
| `sunballProjectileInstance` | `+0x118` | valid SunballProjectile* | SpawnSunball (via Invoke) |
| `currentStep` | `+0x120` | `1` (Charging) | Prefix |
| `currentChargeStepDuration` | `+0x128` | `chargeStepDuration` (+0x58) | Prefix |

Everything else (`sunballCharge`, `shootOnNextChargeStep`, `playerInputDone`, etc.)
was already zeroed/defaulted by `StateEnter`. `UpdateCharging` will pick up with
a clean slate.

---

## Complete Three-Patch Automation (All Confirmed)

```csharp
// ── Patch 1: Suppress "Hold A" instruction text ──────────────────────────────
[HarmonyPatch(typeof(SunboyShootQTESunballState), "BeginDisplayInstructions")]
static class Patch_SunboyQTE_NoInstructions
{
    static bool Prefix() => false;
}

// ── Patch 2: Skip hold-animation, jump straight to charging ──────────────────
[HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateIn")]
static class Patch_SunboyQTE_AutoStart
{
    static bool Prefix(SunboyShootQTESunballState __instance)
    {
        unsafe
        {
            byte* b = (byte*)__instance.Pointer;
            if (*(int*)(b + 0x120) != 0) return true;

            AccessTools.Method(typeof(SunboyShootQTESunballState), "SpawnSunball")
                       .Invoke(__instance, null);
            *(int*)(b + 0x120)   = 1;
            *(float*)(b + 0x128) = *(float*)(b + 0x58);
            return false;
        }
    }
}

// ── Patch 3: Release at peak level automatically ──────────────────────────────
[HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateCharging")]
static class Patch_SunboyQTE_AutoRelease
{
    static void Prefix(SunboyShootQTESunballState __instance)
    {
        unsafe
        {
            byte* b    = (byte*)__instance.Pointer;
            IntPtr proj = *(IntPtr*)(b + 0x118);
            if (proj == IntPtr.Zero) return;

            int  level    = *(int*)(proj + 0xCC);
            int  maxLevel = *(int*)(b + 0xB0);
            bool done     = *(bool*)(b + 0x134);
            bool falling  = *(bool*)(b + 0x124);

            if (level != maxLevel || done || falling) return;

            *(int*)(b + 0xE0)    = 0;     // SuccessPerfect
            *(bool*)(b + 0x134)  = true;  // playerInputDone
            *(float*)(b + 0x128) = -1f;   // expire step timer
        }
    }
}
```
