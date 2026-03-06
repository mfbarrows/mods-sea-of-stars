using System;
using HarmonyLib;

namespace TimedHitMod.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// Moonrang deflect auto-time patches
//
// Strategy: prefix DeflectMoonrangState.GetQTEResult() and call
// OnDeflectProjectile() before grading runs when the move has not yet scored
// enough QTE successes for all targets.
//
// GetQTEResult() reads the `deflecting` field (+0x6C):
//   deflecting == true  -> SuccessBeforeEvent
//   deflecting == false -> FailDidNoPress
// OnDeflectProjectile() (VA 0x180A465A0) sets deflecting = true.
//
// Termination: after RefillAvailableTargetsLeft fires, we read the private
// availableTargets field (offset 0x118 on MoonrangProjectile) via unsafe
// pointer arithmetic to get TargetCount. GetQTEResult then deflects only
// while move.qteSuccessHitCount < TargetCount - 1, letting the extra bounce
// score a natural miss naturally.
// ─────────────────────────────────────────────────────────────────────────────

static class MoonrangCycleFlag
{
    internal static int TargetCount = 0;
    internal static int DeflectCount = 0;
}

/// <summary>
/// Store the MoonrangSpecialMove instance at cast start so GetQTEResult can
/// read qteSuccessHitCount from it without needing a reference via DeflectMoonrangState.
/// Signature: protected virtual void ThrowMoonrang()  Slot 60
/// </summary>
[HarmonyPatch(typeof(MoonrangSpecialMove), "ThrowMoonrang")]
static class Patch_MoonrangSpecialMove_ThrowMoonrang
{
    static void Prefix(MoonrangSpecialMove __instance)
    {
        MoonrangCycleFlag.DeflectCount = 0;
        Plugin.LogI($"[Moonrang.ThrowMoonrang] PRE  | DeflectCount reset, TargetCount={MoonrangCycleFlag.TargetCount} qteSuccessHitCount={__instance.qteSuccessHitCount}");
    }
}

/// <summary>
/// After RefillAvailableTargetsLeft, read availableTargets (private field at
/// offset 0x118 on MoonrangProjectile) to get the true enemy count.
/// This fires on initial setup and on each cycle wrap; TargetCount is stable
/// once set since the enemy list doesn't change mid-fight.
/// </summary>
[HarmonyPatch(typeof(MoonrangProjectile), "RefillAvailableTargetsLeft")]
static class Patch_MoonrangProjectile_RefillAvailableTargetsLeft
{
    static unsafe void Postfix(MoonrangProjectile __instance)
    {
        try
        {
            IntPtr listPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x118);
            if (listPtr != IntPtr.Zero)
            {
                var list = new Il2CppSystem.Collections.Generic.List<CombatTarget>(listPtr);
                MoonrangCycleFlag.TargetCount = list.Count;
                Plugin.LogI($"[Moonrang.RefillAvailableTargetsLeft] POST | TargetCount={MoonrangCycleFlag.TargetCount} from availableTargets");
            }
            else
            {
                Plugin.LogI("[Moonrang.RefillAvailableTargetsLeft] POST | availableTargets ptr was null");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[Moonrang.RefillAvailableTargetsLeft] POST | ERROR reading availableTargets: {ex.Message}");
        }
    }
}

/// <summary>
/// Prefix DeflectMoonrangState.GetQTEResult. Deflect when the move has scored
/// fewer QTE successes than (TargetCount - 1) -- i.e. there are still enemies
/// left to reach via a deflect. Once qteSuccessHitCount >= TargetCount - 1, do
/// nothing so the game scores a natural miss and the chain ends cleanly.
/// Signature: public void GetQTEResult(TeamQTEResult teamQTEResult)
/// </summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.GetQTEResult))]
static class Patch_DeflectMoonrangState_GetQTEResult
{
    static void Prefix(DeflectMoonrangState __instance)
    {
        int deflects = MoonrangCycleFlag.DeflectCount;
        int limit = MoonrangCycleFlag.TargetCount - 1;

        if (deflects >= limit)
        {
            Plugin.LogI(
                $"[Moonrang.GetQTEResult] PRE  | DeflectCount={deflects} >= limit={limit} (TargetCount={MoonrangCycleFlag.TargetCount}) -- natural miss");
            return;
        }

        Plugin.LogI(
            $"[Moonrang.GetQTEResult] PRE  | DeflectCount={deflects} < limit={limit} -- deflecting");
        __instance.OnDeflectProjectile();
        MoonrangCycleFlag.DeflectCount++;
        Plugin.LogI(
            $"[Moonrang.GetQTEResult] PRE  | done (deflecting={__instance.Deflecting} DeflectCount={MoonrangCycleFlag.DeflectCount})");
    }
}
