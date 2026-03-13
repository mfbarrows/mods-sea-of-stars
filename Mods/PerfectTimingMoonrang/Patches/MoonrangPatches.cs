using System;
using HarmonyLib;

namespace PerfectTimingMoonrang.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// Moonrang deflect auto-time patches
//
// Strategy: prefix DeflectMoonrangState.GetQTEResult() and call
// OnDeflectProjectile() before grading runs while LockTracker.EnemiesPendingHit
// is non-empty.
//
// LockTracker maintains EnemiesPendingHit:
//   Init  – all enemies added at move start (RefillAvailableTargetsLeft, first call).
//   Hit   – enemy removed via HitData.SetQTEResult patch (LockTrackingPatches).
//   Locks – OnLocksChanged re-adds enemy if they still have matching locks.
//
// GetQTEResult grants a deflect while the set is non-empty; stops (lets the
// game score a natural miss) when it empties.
// ─────────────────────────────────────────────────────────────────────────────

static class MoonrangCycleFlag
{
    internal static int DeflectCount = 0;
    internal static int TargetCount  = 0;

    // Damage types this move deals (populated once per throw at ThrowMoonrang).
    // Read by LockTracker.UpdateForEnemy to decide which enemies to re-add.
    internal static Il2CppSystem.Collections.Generic.List<EDamageType> MoveDamageTypes = null!;

    /// <summary>
    /// Shared reset logic for both Moonrang and Soonrang ThrowMoonrang patches.
    /// </summary>
    internal static void Reset(MoonrangSpecialMove instance, string tag)
    {
        DeflectCount = 0;
        TargetCount  = 0;

        MoveDamageTypes = new Il2CppSystem.Collections.Generic.List<EDamageType>();
        instance.GetLocksDamageTypes(MoveDamageTypes);

        LockTracker.EnemiesPendingHit.Clear();

        Plugin.LogI(
            $"[{tag}.ThrowMoonrang] PRE  | DeflectCount reset, " +
            $"MoveDamageTypes.Count={MoveDamageTypes.Count}");
    }
}

/// <summary>
/// Reset counters and pending set at move start (Moonrang).
/// Signature: protected virtual void ThrowMoonrang()  Slot 60
/// </summary>
[HarmonyPatch(typeof(MoonrangSpecialMove), "ThrowMoonrang")]
static class Patch_MoonrangSpecialMove_ThrowMoonrang
{
    static void Prefix(MoonrangSpecialMove __instance)
        => MoonrangCycleFlag.Reset(__instance, "Moonrang");
}

/// <summary>
/// Reset counters and pending set at move start (Soonrang).
/// Soonrang overrides ThrowMoonrang at the same vtable slot, so it needs its
/// own patch. The only runtime difference is the damage types returned by
/// GetLocksDamageTypes on the Soonrang instance; all other machinery
/// (RefillAvailableTargetsLeft, GetQTEResult) is shared via the base types.
/// </summary>
[HarmonyPatch(typeof(Soonrang), "ThrowMoonrang")]
static class Patch_Soonrang_ThrowMoonrang
{
    static void Prefix(Soonrang __instance)
        => MoonrangCycleFlag.Reset(__instance, "Soonrang");
}

/// <summary>
/// After RefillAvailableTargetsLeft, read availableTargets (private field at
/// offset 0x118 on MoonrangProjectile) to get the true enemy count.
/// This fires on initial setup and on each cycle wrap; TargetCount is stable.
/// Subsequent refills are the Moonrang cycling back; don't reset the set.
/// </summary>
[HarmonyPatch(typeof(MoonrangProjectile), "RefillAvailableTargetsLeft")]
static class Patch_MoonrangProjectile_RefillAvailableTargetsLeft
{
    static unsafe void Postfix(MoonrangProjectile __instance)
    {
        try
        {
            IntPtr listPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x118);
            if (listPtr == IntPtr.Zero)
            {
                Plugin.LogI("[Moonrang.RefillAvailableTargetsLeft] POST | availableTargets ptr was null");
                return;
            }

            var list = new Il2CppSystem.Collections.Generic.List<CombatTarget>(listPtr);
            Plugin.LogI(
                $"[Moonrang.RefillAvailableTargetsLeft] POST | targets={list.Count} " +
                $"DeflectCount={MoonrangCycleFlag.DeflectCount}");

            // Seed the pending set only on the very first refill (move just started).
            if (MoonrangCycleFlag.DeflectCount == 0)
            {
                LockTracker.InitAllTargets(list, LockTracker.EnemiesPendingHit);
                MoonrangCycleFlag.TargetCount = list.Count;
            }
        }
        catch (Exception ex)
        {
            Plugin.LogI(
                $"[Moonrang.RefillAvailableTargetsLeft] POST | ERROR: {ex.Message}");
        }
    }
}

/// <summary>
/// Prefix DeflectMoonrangState.GetQTEResult. Grants a deflect while
/// LockTracker.EnemiesPendingHit is non-empty; does nothing (natural miss) when empty.
/// Signature: public void GetQTEResult(TeamQTEResult teamQTEResult)
/// </summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.GetQTEResult))]
static class Patch_DeflectMoonrangState_GetQTEResult
{
    static void Prefix(DeflectMoonrangState __instance)
    {
        int deflects = MoonrangCycleFlag.DeflectCount;

        // Safety cap — should never be reached in normal play.
        if (deflects >= 20)
        {
            Plugin.LogW(
                $"[Moonrang.GetQTEResult] PRE  | SAFETY CAP: DeflectCount={deflects} >= 20 -- forcing stop");
            return;
        }

        int pending = LockTracker.EnemiesPendingHit.Count;

        // No enemies left in the pending set.
        if (pending == 0)
        {
            int needed = MoonrangCycleFlag.TargetCount - 1;
            if (deflects < needed)
            {
                // Pending set emptied before we hit every target (e.g. boss arms sharing
                // an owner pointer). Fall back to count-based: keep deflecting.
                Plugin.LogW(
                    $"[Moonrang.GetQTEResult] PRE  | WARNING: pending empty but deflects={deflects} < needed={needed} -- granting fallback deflect");
                __instance.OnDeflectProjectile();
                MoonrangCycleFlag.DeflectCount++;
                return;
            }
            Plugin.LogI(
                $"[Moonrang.GetQTEResult] PRE  | pending set empty (DeflectCount={deflects}) -- natural miss");
            return;
        }

        // Pending enemies exist — grant the deflect.
        Plugin.LogI(
            $"[Moonrang.GetQTEResult] PRE  | pending={pending} DeflectCount={deflects} -- deflecting");
        __instance.OnDeflectProjectile();
        MoonrangCycleFlag.DeflectCount++;
        Plugin.LogI(
            $"[Moonrang.GetQTEResult] PRE  | done (Deflecting={__instance.Deflecting} " +
            $"DeflectCount={MoonrangCycleFlag.DeflectCount})");
    }
}

/// <summary>
/// Skip the instruction text + confirm-input wait for MoonrangSpecialMove.
///
/// Strategy: clear instructionsLocId before the coroutine starts.
/// The coroutine's state-0 checks whether instructionsLocId is valid before
/// showing UI. With the field zeroed, it takes the no-instructions branch
/// directly: Play(ThrowMoonrangIn) → WaitForAnimationDone(ThrowMoonrangLoop)
/// → ThrowMoonrang(). Animation and throw are fully preserved.
///
/// Soonrang.ShowInstructionsAndThrowMoonrangCoroutine (slot 59 override) is a
/// separate compiled method that contains no instruction UI at all — it calls
/// ThrowMoonrang() immediately and yields one frame. No patch needed there.
/// </summary>
[HarmonyPatch(typeof(MoonrangSpecialMove), "ShowInstructionsAndThrowMoonrangCoroutine")]
static class Patch_MoonrangSpecialMove_SkipInstructions
{
    // instructionsLocId is a 16-byte value type (LocalizationId) at offset +0xD8
    // on MoonrangSpecialMove (confirmed from dump.cs).
    // The coroutine checks both 8-byte halves are non-null before showing UI.
    // Zeroing them makes it take the no-instructions path, preserving the
    // animation (Play ThrowMoonrangIn → WaitForAnimationDone → ThrowMoonrang).
    // LocalizationId is in Sabotage.Localization which is not referenced, so
    // we write the field via unsafe pointer rather than assigning the type directly.
    private const int InstructionsLocIdOffset = 0xD8;

    static unsafe void Prefix(MoonrangSpecialMove __instance)
    {
        Plugin.LogI("[Moonrang.ShowInstructions] Clearing instructionsLocId → skipping UI");
        ulong* field = (ulong*)((byte*)__instance.Pointer + InstructionsLocIdOffset);
        field[0] = 0;
        field[1] = 0;
    }
}
