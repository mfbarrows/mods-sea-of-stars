using HarmonyLib;

namespace PerfectTimingArcaneBarrage.Patches;

/// <summary>
/// Skip the instruction text + confirm-input wait for PotionKick.
///
/// Strategy: clear instructionsLocId on DoMove entry (public override, reliably
/// patchable). WaitForSeraiReadyAndThrowPotionsCoroutine state 2 checks both
/// 8-byte halves of instructionsLocId (PotionKick+0xD0, 16-byte LocalizationId
/// value type) before showing UI. Zeroing them makes it take the no-instructions
/// branch directly → wires kickCallback, SetState(seraiKickState), and continues
/// with Reshan throw animations. All kick logic is fully preserved.
///
/// NOTE: WaitForSeraiReadyAndThrowPotionsCoroutine is private (unlike Moonrang's
/// protected virtual), so patching it directly is unreliable in IL2CPP.
/// DoMove (public override) fires before any coroutines start.
/// </summary>
[HarmonyPatch(typeof(PotionKick), "DoMove")]
static class Patch_PotionKick_SkipInstructions
{
    // instructionsLocId is a 16-byte value type (LocalizationId) at offset +0xD0
    // on PotionKick (confirmed from dump.cs: private LocalizationId instructionsLocId; // 0xD0).
    // The coroutine (state 2) checks both 8-byte halves are non-null Il2Cpp objects
    // before entering the UI path. Zeroing them forces the break → common code path.
    private const int InstructionsLocIdOffset = 0xD0;

    static unsafe void Prefix(PotionKick __instance)
    {
        Plugin.LogI("[PotionKick.SkipInstructions] Clearing instructionsLocId → skipping UI");
        ulong* field = (ulong*)((byte*)__instance.Pointer + InstructionsLocIdOffset);
        field[0] = 0;
        field[1] = 0;
    }
}
