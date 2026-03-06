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
/// Signature: protected bool CanAutoTimeHit(PlayerCombatMoveDefinition moveDefinition)
/// </summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanAutoTimeHit")]
static class Patch_CanAutoTimeHit
{
    static void Postfix(ref bool __result)
    {
        __result = true;
    }
}
