using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShowEnemyWeaknesses.Patches;

// HasModifier<ShowEnemyWeaknessesModifier> shares a native address with all other
// HasModifier<T> instantiations — patching it causes infinite recursion.
// Instead, patch CombatInfoBox.ShowEnemyInfo and force-add the sections directly,
// but only when the game hasn't already added them (e.g. via an in-game modifier).
// sectionsInstances is a private field (IL2CPP strips the name) so we read it via
// pointer at offset 0xA0 — the same approach used in ShowEnemyHP.
[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Patch_CombatInfoBox_ShowEnemyInfo
{
    static readonly MethodInfo? _addSection =
        AccessTools.Method(typeof(CombatInfoBox), "AddSection", new[] { typeof(GameObject) });

    static unsafe void Postfix(CombatInfoBox __instance, EnemyCombatTarget enemyTarget)
    {
        if (enemyTarget == null || _addSection == null) return;

        bool hasResistancesSection = false;
        bool hasWeaknessesSection  = false;

        try
        {
            IntPtr listPtr = *(IntPtr*)((byte*)__instance.Pointer + 0xA0);
            if (listPtr != IntPtr.Zero)
            {
                var sections = new Il2CppSystem.Collections.Generic.List<CombatInfoBoxSection>(listPtr);
                for (int i = 0; i < sections.Count; i++)
                {
                    if (sections[i]?.TryCast<CombatInfoBoxResistancesSection>() != null)
                        hasResistancesSection = true;
                    if (sections[i]?.TryCast<CombatInfoBoxWeaknessesSection>() != null)
                        hasWeaknessesSection = true;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ShowEnemyWeaknesses] sectionsInstances read failed: {ex.Message}");
        }

        if (!hasResistancesSection && enemyTarget.HasResistance() && __instance.resistancesSectionPrefab != null)
        {
            try
            {
                var s = (_addSection.Invoke(__instance, new object[] { __instance.resistancesSectionPrefab })
                        as CombatInfoBoxSection)?.TryCast<CombatInfoBoxResistancesSection>();
                s?.Init(enemyTarget);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ShowEnemyWeaknesses] AddSection(resistances) failed: {ex.Message}");
            }
        }

        if (!hasWeaknessesSection && enemyTarget.HasWeakness() && __instance.weaknessesSectionPrefab != null)
        {
            try
            {
                var s = (_addSection.Invoke(__instance, new object[] { __instance.weaknessesSectionPrefab })
                        as CombatInfoBoxSection)?.TryCast<CombatInfoBoxWeaknessesSection>();
                s?.Init(enemyTarget);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ShowEnemyWeaknesses] AddSection(weaknesses) failed: {ex.Message}");
            }
        }
    }
}
