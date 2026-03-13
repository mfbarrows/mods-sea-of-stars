using System;
using HarmonyLib;

namespace PerfectTimingSunball.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// Soonrang Sunball auto-QTE patches
//
// Strategy: simulate player input at the correct moments so the game's own
// UpdateIn / UpdateCharging logic drives the QTE to a natural SuccessPerfect
// outcome — no direct field writes, no method invocations.
//
//   StateEnter     → start simulating A held down.
//   GetButton      → returns true while Holding, false once on Release.
//                    (action name confirmed as "Interact" via log analysis)
//   SetLevel       → flip phase to Release when level == sunballMaxLevel;
//                    fires inside UpdateSunballCharge before GetButton is polled
//                    in the same UpdateCharging frame.
//   OnWentPastMaxCharge → log warning (never observed; kept as sentinel).
//   StateExit      → deactivate unconditionally.
//
// UpdateIn sees GetButton("Interact") == true each frame, waits for the animator
// to reach 100%, then calls BeginCharge() on its own.  SunballProjectile.SetLevel
// fires from UpdateSunballCharge the frame level hits sunballMaxLevel; we flip to
// Release there.  GetButton("Interact") is polled next in the same UpdateCharging
// frame, sees false, writes QTEResult → SuccessPerfect, and calls BeginShoot().
//
// Fields read (IL2CPP native offsets, dump-verified — StateEnter only):
//   SunboyShootQTESunballState:
//     +0x110  SunboyCombatActor*   sunboy
//   PlayerCombatActor (base of SunboyCombatActor):
//     +0x1A8  PlayerInputs*        playerInputs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Shared phase state for the input simulation.</summary>
static class SunballState
{
    internal const string ButtonName = "Interact";
    internal enum Phase { Inactive, Holding, Release }
    internal static Phase Current = Phase.Inactive;
    internal static int MaxLevel = 0;

    // Native pointer of Sunboy's PlayerInputs instance.
    // Compared against __instance.Pointer in the GetButton intercept to ensure
    // we only fake inputs for the correct player, not every InputCategory.
    internal static IntPtr Target = IntPtr.Zero;
}

/// <summary>
/// Patch 1 – Suppress "Hold A" instruction text.
/// BeginDisplayInstructions only drives the TextTyper UI; zero state writes.
/// </summary>
[HarmonyPatch(typeof(SunboyShootQTESunballState), "BeginDisplayInstructions")]
static class Patch_SunboyQTE_NoInstructions
{
    static bool Prefix() => false;
}

/// <summary>
/// Patch 2 – Begin simulating held input on state entry.
/// Reads sunboy (+0x110) → playerInputs (+0x1A8) to capture the target pointer,
/// then sets phase to Holding so GetButton starts returning true.
/// </summary>
[HarmonyPatch(typeof(SunboyShootQTESunballState), "StateEnter")]
static class Patch_SunboyQTE_StateEnter
{
    static void Postfix(SunboyShootQTESunballState __instance)
    {
        unsafe
        {
            IntPtr sunboyPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x110);
            SunballState.Target = sunboyPtr == IntPtr.Zero
                ? IntPtr.Zero
                : *(IntPtr*)(sunboyPtr + 0x1A8);   // PlayerInputs*
        }

        SunballState.MaxLevel = __instance.sunballMaxLevel;
        SunballState.Current = SunballState.Phase.Holding;
        Plugin.LogI($"[QTESunballState] << StateEnter | Phase.Holding maxLevel={SunballState.MaxLevel}");
    }
}

/// <summary>
/// Patch 3 – Deactivate the simulation on state exit.
/// Guards against the state being interrupted before reaching max charge.
/// </summary>
[HarmonyPatch(typeof(SunboyShootQTESunballState), "StateExit")]
static class Patch_SunboyQTE_StateExit
{
    static void Postfix()
    {
        SunballState.Current = SunballState.Phase.Inactive;
        SunballState.Target  = IntPtr.Zero;
        SunballState.MaxLevel = 0;
        Plugin.LogD("[QTESunballState] << StateExit | Phase.Inactive");
    }
}

/// <summary>
/// Patch 4 – Flip to Release when the sunball reaches max level.
/// SetLevel is called by UpdateSunballCharge before GetButton is polled in the
/// same UpdateCharging frame, so the next GetButton("Interact") call sees false
/// and drives the game's own success path: write QTEResult + call BeginShoot.
/// </summary>
[HarmonyPatch(typeof(SunballProjectile), "SetLevel")]
static class Patch_SunboyQTE_SetLevel
{
    static void Postfix(int level)
    {
        if (SunballState.Current != SunballState.Phase.Holding) return;
        if (SunballState.MaxLevel == 0 || level < SunballState.MaxLevel) return;
        SunballState.Current = SunballState.Phase.Release;
        Plugin.LogD($"[SunballProjectile] << SetLevel | reached max level ({level}), Phase.Release");
    }
}

/// <summary>
/// Patch 5 – Log an error if the peak window was missed.
/// OnWentPastMaxCharge fires when qteFullChargeStepDuration expires with no release,
/// meaning our Release signal didn't reach Phase 4 in time.  This should never
/// happen in normal play; the log entry flags a timing bug in the input simulation.
/// </summary>
[HarmonyPatch(typeof(SunboyShootQTESunballState), "OnWentPastMaxCharge")]
static class Patch_SunboyQTE_OnWentPastMaxCharge
{
    static void Postfix()
    {
        Plugin.LogW("[QTESunballState] << OnWentPastMaxCharge | peak window missed — charge will fall!");
    }
}

/// <summary>
/// Patch 6 – Intercept GetButton("Interact") for Sunboy's PlayerInputs only.
/// Returns true (Holding) until SetLevel fires and flips phase to Release,
/// at which point returns false so the game writes SuccessPerfect and calls BeginShoot.
/// </summary>
[HarmonyPatch(typeof(InputCategory), "GetButton")]
static class Patch_SunboyQTE_GetButton
{
    static bool Prefix(InputCategory __instance, string button, ref bool __result)
    {
        if (SunballState.Current == SunballState.Phase.Inactive) return true;
        if (button != SunballState.ButtonName) return true;

        bool isTarget = __instance.Pointer == SunballState.Target;
        if (!isTarget) return true;

        __result = SunballState.Current == SunballState.Phase.Holding;
        Plugin.LogD($"[InputCategory] >> GetButton ({button}) -> forcing {__result}");
        if (SunballState.Current == SunballState.Phase.Release) {
            SunballState.Current = SunballState.Phase.Inactive;
            Plugin.LogD($"[InputCategory] >> GetButton | Phase.Inactive");
        }

        return false;
    }
}
