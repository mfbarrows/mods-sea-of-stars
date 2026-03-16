using System;
using HarmonyLib;

namespace PerfectTimingLeapFrog.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// ArtificerLeapFrog / HeapFrogCombatMove auto-time patches
//
// Strategy: mirrors FanOfKnives exactly.
//
// RefillTargetsAvailable fires when the target list is (re)built.  We read
// targetsAvailable (0x108, List<CombatTarget>) via unsafe pointer to get the
// enemy count, exactly as FanOfKnives reads availableTargets (0x168 on
// SeraiFanOfKnives).
//
// DoMove resets JumpCount to 0 at move start.
//
// CanAutoTimeHit grants auto-time and increments JumpCount while
// the LockTracker.EnemiesPendingHit set is non-empty.  Once every enemy
// has been hit once we stop, the next QTE scores a natural fail, and the
// move's own wind-down fires.
//
// Field offsets (from dump.cs ArtificerLeapFrog):
//   List<CombatTarget> targetsAvailable  0x108
//
// Both ArtificerLeapFrog and HeapFrogCombatMove override DoMove (vtable slot
// 18), so DoMove is patched on both types — same pattern as Moonrang/Soonrang.
// RefillTargetsAvailable is not overridden in HeapFrogCombatMove, so only the
// base-class patch is needed there.
// ─────────────────────────────────────────────────────────────────────────────

static class LeapFrogCycleFlag
{
    internal static int JumpCount   = 0;
    internal static int TargetCount = 0;

    // Damage types this move deals (populated once per use at DoMove).
    // Read by LockTracker.UpdateForEnemy to decide which enemies to re-add.
    internal static Il2CppSystem.Collections.Generic.List<EDamageType> MoveDamageTypes = null!;

    internal static void Reset(ArtificerLeapFrog instance, string tag)
    {
        JumpCount   = 0;
        TargetCount = 0;

        MoveDamageTypes = new Il2CppSystem.Collections.Generic.List<EDamageType>();
        instance.GetLocksDamageTypes(MoveDamageTypes);

        // Clear pending set from any previous move.
        LockTracker.EnemiesPendingHit.Clear();

        Plugin.LogI(
            $"[{tag}.DoMove] PRE  | JumpCount reset, " +
            $"MoveDamageTypes.Count={MoveDamageTypes.Count}");
    }
}

/// <summary>
/// Reset JumpCount at move start so a fresh set of auto-times is granted.
/// Covers the base ArtificerLeapFrog variant.
/// </summary>
[HarmonyPatch(typeof(ArtificerLeapFrog), "DoMove")]
static class Patch_ArtificerLeapFrog_DoMove
{
    static void Prefix(ArtificerLeapFrog __instance)
        => LeapFrogCycleFlag.Reset(__instance, "ArtificerLeapFrog");
}

/// <summary>
/// Reset JumpCount at move start so a fresh set of auto-times is granted.
/// Covers the HeapFrogCombatMove variant (overrides DoMove at vtable slot 18).
/// </summary>
[HarmonyPatch(typeof(HeapFrogCombatMove), "DoMove")]
static class Patch_HeapFrogCombatMove_DoMove
{
    static void Prefix(HeapFrogCombatMove __instance)
        => LeapFrogCycleFlag.Reset(__instance, "HeapFrog");
}

/// <summary>
/// After RefillTargetsAvailable populates the target list, read
/// targetsAvailable (offset 0x108) to capture the true enemy count.
/// Mirrors Patch_SeraiFanOfKnives_RefillAvailableTargetsLeft.
///
/// HeapFrogCombatMove does not override RefillTargetsAvailable, so this
/// single patch on the base class covers both variants.
/// </summary>
[HarmonyPatch(typeof(ArtificerLeapFrog), "RefillTargetsAvailable")]
static class Patch_ArtificerLeapFrog_RefillTargetsAvailable
{
    static unsafe void Postfix(ArtificerLeapFrog __instance)
    {
        try
        {
            IntPtr listPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x108);
            if (listPtr != IntPtr.Zero)
            {
                var list = new Il2CppSystem.Collections.Generic.List<CombatTarget>(listPtr);
                Plugin.LogI(
                    $"[LeapFrog.RefillTargetsAvailable] POST | targets={list.Count} " +
                    $"JumpCount={LeapFrogCycleFlag.JumpCount}");

                // Seed the pending set only on the very first refill (move just started).
                if (LeapFrogCycleFlag.JumpCount == 0)
                {
                    LockTracker.InitAllTargets(list, LockTracker.EnemiesPendingHit);
                    LeapFrogCycleFlag.TargetCount = list.Count;
                }
            }
            else
            {
                Plugin.LogI("[LeapFrog.RefillTargetsAvailable] POST | targetsAvailable ptr was null");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[LeapFrog.RefillTargetsAvailable] POST | ERROR: {ex.Message}");
        }
    }
}

/// <summary>
/// For LeapFrog/HeapFrog moves: grant auto-time while LockTracker.EnemiesPendingHit
/// is non-empty (every enemy still needs a hit), then stop so the move ends naturally.
/// Mirrors Patch_CanAutoTimeHit_FanOfKnives.
/// </summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanAutoTimeHit")]
static class Patch_CanAutoTimeHit_LeapFrog
{
    static void Postfix(PlayerCombatMoveDefinition moveDefinition, ref bool __result)
    {
        if (moveDefinition == null) return;
        string name = moveDefinition.name;
        if (!name.Contains("LeapFrog") && !name.Contains("HeapFrog"))
            return; // other moves handled by PerfectTimingAttack

        int jumps = LeapFrogCycleFlag.JumpCount;

        // Safety cap — should never be reached in normal play.
        if (jumps >= 20)
        {
            __result = false;
            Plugin.LogI(
                $"[CanAutoTimeHit] LeapFrog | SAFETY CAP: JumpCount={jumps} >= 20 -- forcing stop");
            return;
        }

        int pending = LockTracker.EnemiesPendingHit.Count;

        // No enemies left in the pending set.
        if (pending == 0)
        {
            int needed = LeapFrogCycleFlag.TargetCount - 1;
            if (jumps < needed)
            {
                // Pending set emptied before we hit every target (e.g. boss arms sharing
                // an owner pointer). Fall back to count-based: keep granting auto-time.
                __result = true;
                LeapFrogCycleFlag.JumpCount++;
                Plugin.LogW(
                    $"[CanAutoTimeHit] LeapFrog | WARNING: pending empty but jumps={jumps} < needed={needed} -- granting fallback auto-time");
                return;
            }
            __result = false;
            Plugin.LogI(
                $"[CanAutoTimeHit] LeapFrog | pending set empty (JumpCount={jumps}) → stop");
            return;
        }

        // Pending enemies exist — grant auto-time.
        __result = true;
        LeapFrogCycleFlag.JumpCount++;
        Plugin.LogI(
            $"[CanAutoTimeHit] LeapFrog | pending={pending} JumpCount={jumps} → auto-time " +
            $"(JumpCount now {LeapFrogCycleFlag.JumpCount})");
    }
}
