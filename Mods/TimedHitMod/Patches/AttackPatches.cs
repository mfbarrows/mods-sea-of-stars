using HarmonyLib;

namespace TimedHitMod.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// Attack auto-time patches
//
// CanAutoTimeHit (protected, not inlined) is patched directly. Returning true
// activates the game's own auto-time code path in IUpdatableUpdate, which fires
// OnInputPressed at the optimal moment for SuccessPerfect.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Force AbstractTimedAttackHandler.CanAutoTimeHit to always return true.
/// FanOfKnives is excluded here — its own conditional logic lives in
/// Patch_CanAutoTimeHit_FanOfKnives (AutoTimeFanOfKnivesPatches.cs).
/// Signature: protected bool CanAutoTimeHit(PlayerCombatMoveDefinition moveDefinition)
/// </summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanAutoTimeHit")]
static class Patch_CanAutoTimeHit
{
    static void Postfix(PlayerCombatMoveDefinition moveDefinition, ref bool __result)
    {
        if (moveDefinition != null && moveDefinition.name.Contains("FanOfKnives"))
            return; // handled by Patch_CanAutoTimeHit_FanOfKnives
        __result = true;
    }
}
