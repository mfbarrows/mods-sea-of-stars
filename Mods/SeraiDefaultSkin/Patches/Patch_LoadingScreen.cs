using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// LoadingScreen — explicit ROBOT atlas / animator redirect
//
// LoadingScreen has serialized prefab fields for both seraiAtlas and
// seraiRobotAtlas (and serai / seraiRobot animator controllers) that are
// completely separate from CharacterDefinition.requiredImposterAtlas.
//
// GetAtlasReference(CharacterDefinitionId) and
// GetAnimatorControllerReference(CharacterDefinitionId) select between them
// based on the character's CurrentVariant read from save data — which is
// always ROBOT for our save, so they always return the ROBOT assets.
//
// Fix: in a Postfix on Init (fires every time the loading screen is shown),
// copy seraiAtlas → seraiRobotAtlas and serai → seraiRobot so that
// GetAtlasReference / GetAnimatorControllerReference see no difference between
// the two and always render DEFAULT sprites.
//
// NOTE: ImposterAtlasReference and AssetReferenceRuntimeAnimatorController are
// IL2CPP-generated types whose op_Equality calls Il2CppObjectBaseToPtrNotNull
// internally. ALL null checks must box to (object?) to avoid that path.
// ─────────────────────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(LoadingScreen), "Init")]
static class Patch_LoadingScreen_Init
{
    static void Postfix(LoadingScreen __instance)
    {
        // Atlas redirect
        var srcAtlas = (object?)__instance.seraiAtlas;
        var dstAtlas = (object?)__instance.seraiRobotAtlas;
        Plugin.LogD($"[LoadingScreen] Init | seraiAtlas.hash={srcAtlas?.GetHashCode() ?? -1} seraiRobotAtlas.hash={dstAtlas?.GetHashCode() ?? -1}");
        if (srcAtlas != null && !System.Object.ReferenceEquals(srcAtlas, dstAtlas))
        {
            __instance.seraiRobotAtlas = __instance.seraiAtlas;
            Plugin.LogI($"[LoadingScreen] Init | seraiRobotAtlas -> seraiAtlas");
        }

        // Animator controller redirect
        var srcAnim = (object?)__instance.serai;
        var dstAnim = (object?)__instance.seraiRobot;
        Plugin.LogD($"[LoadingScreen] Init | serai.hash={srcAnim?.GetHashCode() ?? -1} seraiRobot.hash={dstAnim?.GetHashCode() ?? -1}");
        if (srcAnim != null && !System.Object.ReferenceEquals(srcAnim, dstAnim))
        {
            __instance.seraiRobot = __instance.serai;
            Plugin.LogI($"[LoadingScreen] Init | seraiRobot -> serai (animator)");
        }
    }
}
