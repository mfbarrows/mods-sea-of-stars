using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShowEnemyHP.Patches;

// HasModifier<ShowEnemyHPModifier> shares a native address with all other
// HasModifier<T> instantiations — patching it causes infinite recursion.
// Instead, patch CombatInfoBox.ShowEnemyInfo and force-add the life bar section.
[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Patch_CombatInfoBox_ShowEnemyInfo
{
    static readonly MethodInfo? _addSection =
        AccessTools.Method(typeof(CombatInfoBox), "AddSection", new[] { typeof(GameObject) });

    static void Postfix(CombatInfoBox __instance, EnemyCombatTarget enemyTarget)
    {
        if (enemyTarget == null || _addSection == null || __instance.lifeBarSectionPrefab == null)
            return;

        var section = (_addSection.Invoke(__instance, new object[] { __instance.lifeBarSectionPrefab })
                      as CombatInfoBoxSection)?.TryCast<CombatInfoBoxLifeBarSection>();
        section?.Init(enemyTarget);
    }
}
