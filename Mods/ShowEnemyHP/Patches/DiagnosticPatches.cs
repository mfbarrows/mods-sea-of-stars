using HarmonyLib;

namespace ShowEnemyHP.Patches;

// CombatInfoBox.ShowEnemyInfo confirmed as the real combat hover entry point.
// These probes log section count + types after ShowEnemyInfo runs so we can
// verify the force-added life bar section appears correctly.

[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Probe_CombatInfoBox_ShowEnemyInfo
{
    static readonly System.Reflection.FieldInfo? _sectionsField =
        AccessTools.Field(typeof(CombatInfoBox), "sectionsInstances");

    static void Postfix(CombatInfoBox __instance)
    {
        var sections = _sectionsField?.GetValue(__instance)
            as Il2CppSystem.Collections.Generic.List<CombatInfoBoxSection>;
        int count = sections?.Count ?? -1;
        Plugin.Log.LogInfo($"[PROBE] ShowEnemyInfo done — sections.Count = {count}");
        if (sections != null)
            for (int i = 0; i < sections.Count; i++)
                Plugin.Log.LogInfo($"[PROBE]   [{i}] {sections[i]?.GetIl2CppType()?.Name ?? "null"}");
    }
}

[HarmonyPatch(typeof(CombatInfoBoxLifeBarSection), "Init")]
static class Probe_CombatInfoBoxLifeBarSection_Init
{
    static void Prefix() => Plugin.Log.LogInfo("[PROBE] CombatInfoBoxLifeBarSection.Init called");
}
