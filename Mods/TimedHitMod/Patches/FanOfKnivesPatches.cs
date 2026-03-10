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
    internal static int ThrowCount  = 0;
    internal static int TargetCount = 0;

    // Damage types this move deals (populated once per use at DoMove).
    // Read by LockTracker.UpdateForEnemy to decide which enemies to re-add.
    internal static Il2CppSystem.Collections.Generic.List<EDamageType> MoveDamageTypes = null!;
}

/// <summary>
/// Reset ThrowCount at move start so a fresh set of auto-times is granted.
/// </summary>
[HarmonyPatch(typeof(SeraiFanOfKnives), "DoMove")]
static class Patch_SeraiFanOfKnives_DoMove
{
    static void Prefix(SeraiFanOfKnives __instance)
    {
        FanOfKnivesCycleFlag.ThrowCount  = 0;
        FanOfKnivesCycleFlag.TargetCount = 0;

        // Capture the damage types this move deals so LockTracker can filter
        // enemies that still have matching spell locks.
        FanOfKnivesCycleFlag.MoveDamageTypes = new Il2CppSystem.Collections.Generic.List<EDamageType>();
        __instance.GetLocksDamageTypes(FanOfKnivesCycleFlag.MoveDamageTypes);

        // Clear pending set from any previous move.
        LockTracker.EnemiesPendingFanOfKnivesHit.Clear();

        Plugin.LogI(
            $"[FanOfKnives.DoMove] PRE  | ThrowCount reset, " +
            $"MoveDamageTypes.Count={FanOfKnivesCycleFlag.MoveDamageTypes.Count}");
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
                Plugin.LogI(
                    $"[FanOfKnives.RefillAvailableTargetsLeft] POST | targets={list.Count} " +
                    $"ThrowCount={FanOfKnivesCycleFlag.ThrowCount}");

                // Seed the pending set only on the very first refill (move just started).
                if (FanOfKnivesCycleFlag.ThrowCount == 0)
                {
                    LockTracker.InitAllTargets(list, LockTracker.EnemiesPendingFanOfKnivesHit);
                    FanOfKnivesCycleFlag.TargetCount = list.Count;
                }
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

        // Safety cap — should never be reached in normal play.
        if (throws >= 20)
        {
            __result = false;
            Plugin.LogI(
                $"[CanAutoTimeHit] FanOfKnives | SAFETY CAP: ThrowCount={throws} >= 20 -- forcing stop");
            return;
        }

        int pending = LockTracker.EnemiesPendingFanOfKnivesHit.Count;

        // No enemies left in the pending set.
        if (pending == 0)
        {
            int needed = FanOfKnivesCycleFlag.TargetCount - 1;
            if (throws < needed)
            {
                // Pending set emptied before we hit every target (e.g. boss arms sharing
                // an owner pointer). Fall back to count-based: keep granting auto-time.
                __result = true;
                FanOfKnivesCycleFlag.ThrowCount++;
                Plugin.LogW(
                    $"[CanAutoTimeHit] FanOfKnives | WARNING: pending empty but throws={throws} < needed={needed} -- granting fallback auto-time");
                return;
            }
            __result = false;
            Plugin.LogI(
                $"[CanAutoTimeHit] FanOfKnives | pending set empty (ThrowCount={throws}) → stop");
            return;
        }

        // Pending enemies exist — grant auto-time.
        __result = true;
        FanOfKnivesCycleFlag.ThrowCount++;
        Plugin.LogI(
            $"[CanAutoTimeHit] FanOfKnives | pending={pending} ThrowCount={throws} → auto-time " +
            $"(ThrowCount now {FanOfKnivesCycleFlag.ThrowCount})");
    }
}

/// <summary>
/// Skip the instruction text + confirm-input wait for all moves that delegate
/// to PlayerCombatMove.ShowInstructions.
/// Confirmed callers: SeraiFanOfKnives (OpenPortalsCoroutine),
///                    ConflagrateCombatMove (InstructionsCoroutine).
/// Also covers any other PlayerCombatMove subclass that uses the same base method.
/// </summary>
[HarmonyPatch(typeof(PlayerCombatMove), "ShowInstructions")]
static class Patch_PlayerCombatMove_SkipInstructions
{
    static bool Prefix(PlayerCombatMove __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        Plugin.LogI($"[ShowInstructions] Skipping for {__instance.GetType().Name}");
        __result = new Il2CppSystem.Collections.ArrayList(0).GetEnumerator();
        return false;
    }
}
