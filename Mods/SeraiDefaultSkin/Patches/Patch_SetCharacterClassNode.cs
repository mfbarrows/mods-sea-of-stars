using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// DIAGNOSTIC: SetCharacterClassNode.BeforeExecute fires when a behaviour-tree node
/// is about to switch a party character's class (also calls SwapPartyCharacterGameObject).
/// Logs target characters mask, class, and reloadParty flag.
/// Signature: public override void BeforeExecute()
/// </summary>
[HarmonyPatch(typeof(SetCharacterClassNode), "BeforeExecute")]
static class Patch_SetCharacterClassNode_BeforeExecute
{
    static void Prefix(SetCharacterClassNode __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[SetCharacterClassNode] >> BeforeExecute | " +
            $"toSet={__instance.toSet?.Value} " +
            $"class={__instance.characterClass?.Value?.GetType().Name ?? "null"} " +
            $"reloadParty={__instance.reloadParty?.Value}");
    }
}
