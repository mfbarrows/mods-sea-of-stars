using HarmonyLib;

namespace ShowEnemyHP.Patches;

/// <summary>
/// Forces EnemyDescriptionPanel.MustShowHP() to always return true,
/// making enemy HP bars visible without the Abacus equipped.
///
/// MustShowHP() normally calls:
///   Manager&lt;CharacterStatsManager&gt;.Instance
///       .HasModifierOfType&lt;ShowEnemyHPModifier&gt;(includeGlobal: true)
/// which only returns true when the Abacus is in a party member's equipment slot.
/// Patching the return value bypasses that check entirely.
/// </summary>
[HarmonyPatch(typeof(EnemyDescriptionPanel), "MustShowHP")]
static class Patch_EnemyDescriptionPanel_MustShowHP
{
    static void Postfix(ref bool __result) => __result = true;
}
