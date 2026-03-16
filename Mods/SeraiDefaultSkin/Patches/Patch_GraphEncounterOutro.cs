using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// GraphEncounterOutro.GiveControlBackToPlayer — override that starts the
/// BehaviourTree/cutscene controller (treeController). This is where the
/// ROBOT-class-switch BT node lives during the final-boss encounter.
/// restorePartySetting and waitForTreeDone are logged to understand sequencing.
/// </summary>
[HarmonyPatch(typeof(GraphEncounterOutro), "GiveControlBackToPlayer")]
static class Patch_GraphEncounterOutro_GiveControlBackToPlayer
{
    static void Prefix(GraphEncounterOutro __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[GraphEncounterOutro] >> GiveControlBackToPlayer | " +
            $"type={__instance.GetType().Name} " +
            $"waitForTreeDone={__instance.waitForTreeDone} " +
            $"treeController={__instance.treeController?.GetType().Name ?? "null"}");
    }
}

/// <summary>
/// GraphEncounterOutro.DoTreeDoneBehaviour — fires when the BT finishes.
/// onTreeDoneBehaviour is a private flag field; we read it via Traverse.
/// This fires between the BT completing and control actually returning,
/// so if the ROBOT swap is still in the BT this will come after it.
/// </summary>
[HarmonyPatch(typeof(GraphEncounterOutro), "DoTreeDoneBehaviour")]
static class Patch_GraphEncounterOutro_DoTreeDoneBehaviour
{
    static void Prefix(GraphEncounterOutro __instance)
    {
        if (!Diag.Enabled) return;
        var onTreeDone = Traverse.Create(__instance).Field("onTreeDoneBehaviour").GetValue();
        Plugin.LogD($"[GraphEncounterOutro] >> DoTreeDoneBehaviour | " +
            $"type={__instance.GetType().Name} onTreeDoneBehaviour={onTreeDone}");
    }
}
