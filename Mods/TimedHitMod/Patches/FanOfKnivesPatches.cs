using System;
using HarmonyLib;

namespace TimedHitMod.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// FanOfKnives auto-time patches
//
// Strategy: mirrors Moonrang exactly.
//
// RefillAvailableTargetsLeft fires when the target list is (re)built.  We read
// availableTargets (0x168, List<CombatTarget>) via unsafe pointer to get the
// enemy count, exactly as Moonrang reads its own availableTargets (0x118 on
// MoonrangProjectile).
//
// DoMove resets ThrowCount to 0 at move start.
//
// CanAutoTimeHit grants auto-time and increments ThrowCount while
// ThrowCount < TargetCount.  Once every enemy has been hit once we stop,
// the next QTE scores a natural fail, and ExitNextPortal's wind-down fires:
//   qteFailed && freeJumps <= hitCount  →  WaitAndExitFirstPortalCoroutine
//
// Field offsets (from dump.cs SeraiFanOfKnives):
//   List<CombatTarget> availableTargets  0x168
// ─────────────────────────────────────────────────────────────────────────────

static class FanOfKnivesCycleFlag
{
    internal static int TargetCount = 0;
    internal static int ThrowCount  = 0;
}

/// <summary>
/// Reset ThrowCount at move start so a fresh set of auto-times is granted.
/// </summary>
[HarmonyPatch(typeof(SeraiFanOfKnives), "DoMove")]
static class Patch_SeraiFanOfKnives_DoMove
{
    static void Prefix()
    {
        FanOfKnivesCycleFlag.ThrowCount = 0;
        Plugin.LogI(
            $"[FanOfKnives.DoMove] PRE  | ThrowCount reset, TargetCount={FanOfKnivesCycleFlag.TargetCount}");
    }
}

/// <summary>
/// After RefillAvailableTargetsLeft populates the target list, read
/// availableTargets (offset 0x168) to capture the true enemy count.
/// Mirrors Patch_MoonrangProjectile_RefillAvailableTargetsLeft.
/// </summary>
[HarmonyPatch(typeof(SeraiFanOfKnives), "RefillAvailableTargetsLeft")]
static class Patch_SeraiFanOfKnives_RefillAvailableTargetsLeft
{
    static unsafe void Postfix(SeraiFanOfKnives __instance)
    {
        try
        {
            IntPtr listPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x168);
            if (listPtr != IntPtr.Zero)
            {
                var list = new Il2CppSystem.Collections.Generic.List<CombatTarget>(listPtr);
                FanOfKnivesCycleFlag.TargetCount = list.Count;
                Plugin.LogI(
                    $"[FanOfKnives.RefillAvailableTargetsLeft] POST | TargetCount={FanOfKnivesCycleFlag.TargetCount}");
            }
            else
            {
                Plugin.LogI("[FanOfKnives.RefillAvailableTargetsLeft] POST | availableTargets ptr was null");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[FanOfKnives.RefillAvailableTargetsLeft] POST | ERROR: {ex.Message}");
        }
    }
}

/// <summary>
/// For FanOfKnives moves: grant auto-time while ThrowCount &lt; TargetCount
/// (every enemy still needs a hit), then stop so the move ends naturally.
/// Mirrors Patch_DeflectMoonrangState_GetQTEResult.
/// </summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanAutoTimeHit")]
static class Patch_CanAutoTimeHit_FanOfKnives
{
    static void Postfix(PlayerCombatMoveDefinition moveDefinition, ref bool __result)
    {
        if (moveDefinition == null || !moveDefinition.name.Contains("FanOfKnives"))
            return; // other moves handled by Patch_CanAutoTimeHit in AutoTimeAttackPatches.cs

        int throws  = FanOfKnivesCycleFlag.ThrowCount;
        int targets = FanOfKnivesCycleFlag.TargetCount;

        if (throws < targets)
        {
            __result = true;
            FanOfKnivesCycleFlag.ThrowCount++;
            Plugin.LogI(
                $"[CanAutoTimeHit] FanOfKnives | ThrowCount={throws} < TargetCount={targets} → auto-time (ThrowCount now {FanOfKnivesCycleFlag.ThrowCount})");
        }
        else
        {
            // All enemies hit once — natural fail ends the move.
            Plugin.LogI(
                $"[CanAutoTimeHit] FanOfKnives | ThrowCount={throws} >= TargetCount={targets} → stop, let move end");
        }
    }
}
