using HarmonyLib;

namespace PerfectTimingAttack.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// Attack auto-time patches
//
// CanAutoTimeHit (protected, not inlined) is patched directly. Returning true
// activates the game's own auto-time code path in IUpdatableUpdate, which fires
// OnInputPressed at the optimal moment for SuccessPerfect.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Force AbstractTimedAttackHandler.CanAutoTimeHit to always return true.
/// FanOfKnives is excluded here — PerfectTimingVenomFlurry handles its own
/// count-bounded auto-time logic for that move.
/// Signature: protected bool CanAutoTimeHit(PlayerCombatMoveDefinition moveDefinition)
/// </summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanAutoTimeHit")]
static class Patch_CanAutoTimeHit
{
    static void Postfix(PlayerCombatMoveDefinition moveDefinition, ref bool __result)
    {
        if (moveDefinition != null && moveDefinition.name.Contains("FanOfKnives"))
            return; // handled by PerfectTimingVenomFlurry
        if (moveDefinition != null &&
            (moveDefinition.name.Contains("LeapFrog") || moveDefinition.name.Contains("HeapFrog")))
            return; // handled by PerfectTimingLeapFrog
        if (moveDefinition != null &&
            (moveDefinition.name.Contains("Jugglenaut") || moveDefinition.name.Contains("Jugglecore")))
            return; // handled by PerfectTimingJugglenaut
        __result = true;
    }
}
