# `SunboyShootQTESunballState.BeginIn` — Analysis

## Summary

`BeginIn` initialises the `EShootSunballStep.In (= 0)` phase. It writes
**exactly one state field** (`currentStep = 0`) and then sets up the animator
in a frozen initial pose. It is called from `StateEnter` to encapsulate the
animation setup for the "waiting for player to hold A" phase.

**Mod relevance: none for basic auto-release. For auto-start, confirming
`currentStep = 0` on entry is correct so the `UpdateIn` Prefix fires
predictably.**

---

## Pointer Arithmetic Key

`param_1` = `this` as `longlong*`, so `param_1 + n` as address = `base + n×8`
when cast.

| Expression | Byte offset | C# field |
|---|---|---|
| `*(undefined4 *)(param_1 + 0x24)` | `+0x120` | `currentStep` (EShootSunballStep) |
| `param_1[0x22]` | `+0x110` | `sunboy` (SunboyCombatActor*) |
| `*(longlong **)(sunboy + 0x78)` | `sunboy+0x78` | `sunboy.animator` object (Animator) |

---

## `SunboyAnims` Static Fields (Confirmed from Dump)

```csharp
public static class SunboyAnims
{
    public static readonly int Heal;                      // 0x0  (offset into static block)
    public static readonly int RaiseSword;                // 0x4
    public static readonly int SunballIn;                 // 0x8  ← BeginIn plays this
    public static readonly int SunballCharge;             // 0xC
    public static readonly int SunballShoot;              // 0x10
    public static readonly int DashStrikeIn;              // 0x14
    public static readonly int DashStrikeLoop;            // 0x18
    public static readonly int DashStrikeOut;             // 0x1C
    public static readonly int SuperSolsticeStrikeHit1;   // 0x20
    ...
}
```

`SunboyAnims_TypeInfo[0x17]` is the pointer to the static fields block.
`*(uint *)(SunboyAnims_TypeInfo[0x17] + 8)` = byte offset 8 = `SunboyAnims.SunballIn`.

This is also the same hash played by `StateEnter`'s inline animator setup at the
very end — confirming `BeginIn` does the same thing as that inline block.

---

## Annotated C# Reconstruction

```csharp
private void BeginIn()
{
    // ── 1. Set step to In ────────────────────────────────────────────────────
    currentStep = EShootSunballStep.In;   // +0x120 = 0

    // ── 2. Set up frozen initial pose ────────────────────────────────────────
    // Freeze the animator speed so it doesn't auto-advance.
    // Play the SunballIn animation from the beginning on all layers.
    // Step one frame to commit the first pose to the renderer.
    Animator anim = sunboy.animator;
    anim.speed = 0f;
    anim.Play(SunboyAnims.SunballIn, -1);   // hash 0x8, layer -1 (all layers)
    anim.Update(0f);
}
```

The `SunballIn` animation is the "sunball being pulled toward Sunboy's body" clip,
frozen at frame 0 — the static initial pose shown while the player is asked to hold A.

---

## Call Site: `StateEnter`

`BeginIn` writes exactly what `StateEnter`'s inline animation block does at the
end (same speed=0, Play, Update sequence with the same hash). This confirms
`BeginIn` is the extracted helper called from `StateEnter`, not from `StateExecute`.

```
StateEnter()
    [field resets]
    GatherAdditionalPlayers()
    BeginDisplayInstructions()
    BeginIn()         ← sets currentStep=0, frozen SunballIn pose
```

Note: `StateEnter`'s closing animation code and `BeginIn`'s code are essentially
identical. Ghidra may show this as `StateEnter` calling `BeginIn` or partially
inlining it. Either way the effect is: by the time `StateEnter` returns,
`currentStep == 0` and the animator is frozen at the `SunballIn` entry frame.

---

## Relationship to `UpdateIn`

`BeginIn` sets `currentStep = 0`, which causes `StateExecute`'s dispatch to
call `UpdateIn` every frame. `UpdateIn` reads input and advances the animation.
No other initialisation is needed between `BeginIn` and the first `UpdateIn` call.

---

## Mod Implication

### Auto-start Patch — `currentStep` is 0 when `UpdateIn` Prefix fires

`BeginIn` confirms `currentStep = 0` is the entry state. Our `UpdateIn` Prefix
checks `currentStep == 0` before acting — this is safe.
`BeginIn` itself does NOT need to be patched.

### `SunballIn` animation on entry

When the auto-start patch fires (Prefix on `UpdateIn` → skip to charging), the
animator is frozen on frame 0 of `SunballIn`. The patch calls `SpawnSunball()`
and sets `currentStep = 1` — on the next frame `StateExecute` dispatches to
`UpdateCharging`, where the charging animation/SFX are driven from the sunball
projectile itself. The frozen entry pose is simply replaced immediately without
any visual glitch in practice (one frozen frame).

---

## Full State Transition Sequence (Confirmed)

```
StateEnter()
    → GatherAdditionalPlayers()
    → BeginDisplayInstructions()    ← pure UI, suppress with Prefix→false
    → BeginIn()                     ← currentStep=0, animator frozen

[UpdateIn frames]
    poll Attack button
    advance SunballIn animation proportionally
    on completion →
        currentStep = 1
        currentChargeStepDuration = chargeStepDuration
        SpawnSunball()  ← creates sunballProjectileInstance

[UpdateCharging frames]
    charge loop  ← AUTO-RELEASE MOD acts here
    at peak level: AutoRelease() writes 3 fields

[BeginShoot]
    success path → onSunballReady → ThrowSunball → OnShootSunball → projectile launched
```

### Auto-start Patch (Confirmed Clean)

```csharp
[HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateIn")]
static class Patch_UpdateIn_AutoStart
{
    static bool Prefix(SunboyShootQTESunballState __instance)
    {
        unsafe
        {
            byte* b = (byte*)__instance.Pointer;
            if (*(int*)(b + 0x120) != 0) return true;   // not In step, run original

            // Set step to Charging (1)
            *(int*)(b + 0x120)   = 1;
            // Load first charge step timer
            *(float*)(b + 0x128) = *(float*)(b + 0x58);  // chargeStepDuration

            // Spawn the sunball — mandatory before UpdateCharging can run
            AccessTools.Method(typeof(SunboyShootQTESunballState), "SpawnSunball")
                       .Invoke(__instance, null);

            return false;   // suppress UpdateIn
        }
    }
}
```
