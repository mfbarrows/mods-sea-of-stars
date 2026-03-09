using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShowEnemyHP.Patches;

// HasModifier<ShowEnemyHPModifier> shares a native address with all other
// HasModifier<T> instantiations — patching it causes infinite recursion.
// Instead, patch CombatInfoBox.ShowEnemyInfo and force-add the life bar section,
// but only when ShowEnemyInfo hasn't already added one (e.g. via an in-game
// ShowEnemyHP modifier). The game keeps its sections in the private
// sectionsInstances field (offset 0xA0, List<CombatInfoBoxSection>) — IL2CPP
// strips private field names so we read via pointer arithmetic.
[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Patch_CombatInfoBox_ShowEnemyInfo
{
    static readonly MethodInfo? _addSection =
        AccessTools.Method(typeof(CombatInfoBox), "AddSection", new[] { typeof(GameObject) });

    static unsafe void Postfix(CombatInfoBox __instance, EnemyCombatTarget enemyTarget)
    {
        if (enemyTarget == null || _addSection == null || __instance.lifeBarSectionPrefab == null)
            return;

        // Read the private sectionsInstances field at offset 0xA0 via pointer.
        // If any existing section is a CombatInfoBoxLifeBarSection the game
        // already added one (ShowEnemyHPModifier active) — skip to avoid duplicate.
        try
        {
            IntPtr listPtr = *(IntPtr*)((byte*)__instance.Pointer + 0xA0);
            if (listPtr != IntPtr.Zero)
            {
                var sections = new Il2CppSystem.Collections.Generic.List<CombatInfoBoxSection>(listPtr);
                for (int i = 0; i < sections.Count; i++)
                    if (sections[i]?.TryCast<CombatInfoBoxLifeBarSection>() != null)
                        return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ShowEnemyHP] sectionsInstances read failed: {ex.Message}");
        }

        var section = (_addSection.Invoke(__instance, new object[] { __instance.lifeBarSectionPrefab })
                      as CombatInfoBoxSection)?.TryCast<CombatInfoBoxLifeBarSection>();
        section?.Init(enemyTarget);
    }
}
