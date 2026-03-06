using HarmonyLib;

namespace ShowEnemyWeaknesses.Patches;

/// <summary>
/// Forces EnemyDescriptionPanel.MustShowWeaknesses() to always return true,
/// making enemy weakness/strength icons visible without the Studious Toadle
/// or any other weakness-revealing equipment equipped.
///
/// MustShowWeaknesses() is the sibling of MustShowHP() and follows the same
/// pattern: it calls CharacterStatsManager.HasModifierOfType&lt;ShowEnemyWeaknessesModifier&gt;
/// (includeGlobal: true). Forcing the return value bypasses that check.
/// </summary>
[HarmonyPatch(typeof(EnemyDescriptionPanel), "MustShowWeaknesses")]
static class Patch_EnemyDescriptionPanel_MustShowWeaknesses
{
    static void Postfix(ref bool __result) => __result = true;
}
