using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShowEnemyWeaknesses.Patches;

// HasModifierOfType<ShowEnemyWeaknessesModifier> is inlined into
// CombatInfoBox.ShowEnemyInfo — the correct target confirmed by log probe.
// We bypass the modifier check by force-adding resistances/weaknesses sections
// in a Postfix when the original code skipped them.
[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Patch_CombatInfoBox_ShowEnemyInfo
{
    static readonly MethodInfo? _addSection =
        AccessTools.Method(typeof(CombatInfoBox), "AddSection", new[] { typeof(GameObject) });

    static readonly FieldInfo? _sectionsField =
        AccessTools.Field(typeof(CombatInfoBox), "sectionsInstances");

    static void Postfix(CombatInfoBox __instance, EnemyCombatTarget enemyTarget)
    {
        if (enemyTarget == null || _addSection == null) return;

        // Check which sections were already added by the original code
        // (modifier present → already there; modifier absent + our fix → we add them).
        bool hasResistances = false;
        bool hasWeaknesses  = false;
        var sections = _sectionsField?.GetValue(__instance)
            as Il2CppSystem.Collections.Generic.List<CombatInfoBoxSection>;
        if (sections != null)
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i]?.TryCast<CombatInfoBoxResistancesSection>() != null) hasResistances = true;
                if (sections[i]?.TryCast<CombatInfoBoxWeaknessesSection>()  != null) hasWeaknesses  = true;
            }

        // Add resistances section if enemy has resistances and it wasn't already shown.
        if (!hasResistances && enemyTarget.HasResistance() &&
            __instance.resistancesSectionPrefab != null)
        {
            var s = (_addSection.Invoke(__instance, new object[] { __instance.resistancesSectionPrefab })
                    as CombatInfoBoxSection)?.TryCast<CombatInfoBoxResistancesSection>();
            s?.Init(enemyTarget);
        }

        // Add weaknesses section if enemy has weaknesses and it wasn't already shown.
        if (!hasWeaknesses && enemyTarget.HasWeakness() &&
            __instance.weaknessesSectionPrefab != null)
        {
            var s = (_addSection.Invoke(__instance, new object[] { __instance.weaknessesSectionPrefab })
                    as CombatInfoBoxSection)?.TryCast<CombatInfoBoxWeaknessesSection>();
            s?.Init(enemyTarget);
        }
    }
}
