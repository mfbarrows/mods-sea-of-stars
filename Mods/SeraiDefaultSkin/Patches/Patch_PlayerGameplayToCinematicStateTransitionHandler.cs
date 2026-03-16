using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// PlayerGameplayToCinematicStateTransitionHandler — cinematic ↔ gameplay state
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Fires when a character's state-machine is switched to cinematic state
/// (animation locked, no player input). Called from GraphEncounterOutro and
/// cutscene BT nodes when they take over a character.
/// </summary>
[HarmonyPatch(typeof(PlayerGameplayToCinematicStateTransitionHandler),
    nameof(PlayerGameplayToCinematicStateTransitionHandler.EnterCinematicState))]
static class Patch_PlayerGameplayToCinematicStateTransitionHandler_EnterCinematicState
{
    static void Prefix(PlayerGameplayToCinematicStateTransitionHandler __instance)
    {
        if (!Diag.Enabled) return;
        var charId = __instance.player?.characterDefinitionId.characterId ?? "null";
        Plugin.LogD($"[PlayerGameplayToCinematicStateTransitionHandler] >> EnterCinematicState | charId={charId}");
    }
}

/// <summary>
/// Fires when a character's state-machine is restored to gameplay (post-cutscene).
/// allowedStates=0 means all states allowed (default). This is the point where
/// the variant-dependent gameplay model re-activates — prime suspect for where
/// ROBOT visuals re-emerge after the cutscene.
/// </summary>
[HarmonyPatch(typeof(PlayerGameplayToCinematicStateTransitionHandler),
    nameof(PlayerGameplayToCinematicStateTransitionHandler.EnterGameplayState))]
static class Patch_PlayerGameplayToCinematicStateTransitionHandler_EnterGameplayState
{
    static void Prefix(PlayerGameplayToCinematicStateTransitionHandler __instance, EPlayerState allowedStates)
    {
        if (!Diag.Enabled) return;
        var charId = __instance.player?.characterDefinitionId.characterId ?? "null";
        Plugin.LogD($"[PlayerGameplayToCinematicStateTransitionHandler] >> EnterGameplayState | charId={charId} allowedStates={allowedStates}");
    }
}
