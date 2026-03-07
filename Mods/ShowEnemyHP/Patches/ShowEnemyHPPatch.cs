using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShowEnemyHP.Patches;

// The real combat hover box is CombatInfoBox — confirmed by log probe.
// HasModifierOfType<ShowEnemyHPModifier> is inlined into ShowEnemyInfo;
// we bypass it by force-adding the CombatInfoBoxLifeBarSection in a Postfix
// when the original code chose not to.
[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Patch_CombatInfoBox_ShowEnemyInfo
{
    static readonly MethodInfo? _addSection =
        AccessTools.Method(typeof(CombatInfoBox), "AddSection", new[] { typeof(GameObject) });

    static readonly FieldInfo? _sectionsField =
        AccessTools.Field(typeof(CombatInfoBox), "sectionsInstances");

    // Experiment: try to obtain the *closed* generic HasModifier<ShowEnemyHPModifier>
    // from GameplayModifierHandler to learn whether IL2CPP emitted that instantiation
    // or collapsed everything to <object>.
    static readonly MethodInfo? _hasModifierOpen =
        AccessTools.Method(typeof(GameplayModifierHandler), "HasModifier");
    static readonly MethodInfo? _hasModifierClosed =
        _hasModifierOpen?.MakeGenericMethod(typeof(ShowEnemyHPModifier));

    static void Postfix(CombatInfoBox __instance, EnemyCombatTarget enemyTarget)
    {
        // ---- Closed-generic experiment ----------------------------------------
        try
        {
            if (_hasModifierClosed == null)
                Plugin.Log.LogWarning("[ShowEnemyHP] HasModifier<ShowEnemyHPModifier>: MakeGenericMethod returned null");
            else
            {
                Plugin.Log.LogInfo($"[ShowEnemyHP] HasModifier<ShowEnemyHPModifier> MethodHandle: {_hasModifierClosed.MethodHandle.Value:X}");
                Plugin.Log.LogInfo($"[ShowEnemyHP] ContainsGenericParameters: {_hasModifierClosed.ContainsGenericParameters}");
                bool patchable = !_hasModifierClosed.ContainsGenericParameters &&
                                 _hasModifierClosed.MethodHandle.Value != IntPtr.Zero;
                Plugin.Log.LogInfo($"[ShowEnemyHP] Patchable (heuristic): {patchable}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ShowEnemyHP] Closed-generic reflection threw: {ex.GetType().Name}: {ex.Message}");
        }
        // -----------------------------------------------------------------------

        if (enemyTarget == null || _addSection == null || __instance.lifeBarSectionPrefab == null)
            return;

        // If the original code already added a life bar section (modifier was present
        // and hideHP was false), don't add a duplicate.
        var sections = _sectionsField?.GetValue(__instance)
            as Il2CppSystem.Collections.Generic.List<CombatInfoBoxSection>;
        if (sections != null)
            for (int i = 0; i < sections.Count; i++)
                if (sections[i]?.TryCast<CombatInfoBoxLifeBarSection>() != null)
                    return;

        // Modifier check failed or hideHP was set — force-add the life bar section.
        var section = (_addSection.Invoke(__instance, new object[] { __instance.lifeBarSectionPrefab })
                      as CombatInfoBoxSection)?.TryCast<CombatInfoBoxLifeBarSection>();
        section?.Init(enemyTarget);
    }
}
