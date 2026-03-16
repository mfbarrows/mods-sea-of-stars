using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// Debug log: records every C#-side read of Serai's current variant.
/// Confirmed: this is NEVER called — all callers read the backing field at 0x4C
/// directly via native IL2CPP field access. Kept as a canary; if it ever fires
/// it means something changed and the getter is now reachable from C# code.
/// Signature: public EPartyCharacterVariant get_CurrentVariant()
/// </summary>
[HarmonyPatch(typeof(PlayableCharacterData), nameof(PlayableCharacterData.CurrentVariant), MethodType.Getter)]
static class Patch_PlayableCharacterData_get_CurrentVariant
{
    static void Postfix(PlayableCharacterData __instance, EPartyCharacterVariant __result)
    {
        if (!Diag.Enabled) return;
        if (__instance.characterId == CharacterDefinitionId.Serai)
            Plugin.LogD($"[PlayableCharacterData] >> get_CurrentVariant | result={__result}");
    }
}

/// <summary>
/// Debug log: records every SetVariant call for Serai — variant written,
/// reloadMoveSet flag, and load flag.
/// Signature: public void SetVariant(EPartyCharacterVariant variant,
///   bool reloadMoveSet = false, bool load = true)
/// </summary>
[HarmonyPatch(typeof(PlayableCharacterData), nameof(PlayableCharacterData.SetVariant))]
static class Patch_PlayableCharacterData_SetVariant
{
    static void Prefix(PlayableCharacterData __instance, EPartyCharacterVariant variant, bool reloadMoveSet, bool load)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayableCharacterData] >> SetVariant | " +
            $"prev={__instance.CurrentVariant} → next={variant} | " +
            $"reloadMoveSet={reloadMoveSet} load={load}");
    }
}
