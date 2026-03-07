using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShowEnemyWeaknesses.Patches;

// HasModifier<ShowEnemyWeaknessesModifier> shares a native address with all other
// HasModifier<T> instantiations — patching it causes infinite recursion.
// Instead, patch CombatInfoBox.ShowEnemyInfo and force-add the sections directly.
[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Patch_CombatInfoBox_ShowEnemyInfo
{
    static readonly MethodInfo? _addSection =
        AccessTools.Method(typeof(CombatInfoBox), "AddSection", new[] { typeof(GameObject) });

    static void Postfix(CombatInfoBox __instance, EnemyCombatTarget enemyTarget)
    {
        if (enemyTarget == null || _addSection == null) return;

        if (enemyTarget.HasResistance() && __instance.resistancesSectionPrefab != null)
        {
            var s = (_addSection.Invoke(__instance, new object[] { __instance.resistancesSectionPrefab })
                    as CombatInfoBoxSection)?.TryCast<CombatInfoBoxResistancesSection>();
            s?.Init(enemyTarget);
        }

        if (enemyTarget.HasWeakness() && __instance.weaknessesSectionPrefab != null)
        {
            var s = (_addSection.Invoke(__instance, new object[] { __instance.weaknessesSectionPrefab })
                    as CombatInfoBoxSection)?.TryCast<CombatInfoBoxWeaknessesSection>();
            s?.Init(enemyTarget);
        }
    }
}
