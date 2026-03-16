using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// Encounter.ReturnCharactersToGameplayState — private, fires during encounter
/// cleanup. Calls SetCharactersIntoGameplayState on the party, which can
/// restore class/variant settings from save data.
/// </summary>
[HarmonyPatch(typeof(Encounter), "ReturnCharactersToGameplayState")]
static class Patch_Encounter_ReturnCharactersToGameplayState
{
    static void Prefix(Encounter __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[Encounter] >> ReturnCharactersToGameplayState | type={__instance.GetType().Name}");
    }
}
