using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// EncounterOutro.GiveControlBackToPlayer — base class virtual, fires after
/// outro rewards and level-ups are done. Subclasses override to run a BT or
/// cutscene before truly giving control back.
/// </summary>
[HarmonyPatch(typeof(EncounterOutro), "GiveControlBackToPlayer")]
static class Patch_EncounterOutro_GiveControlBackToPlayer
{
    static void Prefix(EncounterOutro __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[EncounterOutro] >> GiveControlBackToPlayer | type={__instance.GetType().Name}");
    }
}
