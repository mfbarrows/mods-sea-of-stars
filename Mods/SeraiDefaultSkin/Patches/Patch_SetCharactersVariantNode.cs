using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// DIAGNOSTIC: SetCharactersVariantNode.BeforeExecute fires when a behaviour-tree
/// node is about to swap party characters to a new variant (e.g. Serai → ROBOT
/// during the final-boss cutscene). Logs the target characters mask and variant so
/// we can confirm this is the trigger for the unwanted ROBOT swap.
/// Signature: public override void BeforeExecute()
/// </summary>
[HarmonyPatch(typeof(SetCharactersVariantNode), "BeforeExecute")]
static class Patch_SetCharactersVariantNode_BeforeExecute
{
    static void Prefix(SetCharactersVariantNode __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[SetCharactersVariantNode] >> BeforeExecute | " +
            $"toSet={__instance.toSet?.Value} " +
            $"variant={__instance.variant?.Value} " +
            $"reloadParty={__instance.reloadParty?.Value}");
    }
}
