using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// CharacterStatsManager — variant change entry point
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Fires when ANY code path asks to change a list of characters' variant.
/// First param is List&lt;CharacterDefinitionId&gt; (class) — patchable.
/// The singular SetCharacterVariant(CharacterDefinitionId, ...) has a struct
/// first param and is NOT patchable.
/// </summary>
[HarmonyPatch(typeof(CharacterStatsManager), nameof(CharacterStatsManager.SetCharactersVariant))]
static class Patch_CharacterStatsManager_SetCharactersVariant
{
    static void Prefix(Il2CppSystem.Collections.Generic.List<CharacterDefinitionId> characters, EPartyCharacterVariant variant,
        bool updateCharacters)
    {
        if (!Diag.Enabled) return;
        var ids = characters == null ? "null" : string.Join(",", characters);
        Plugin.LogD($"[CharacterStatsManager] >> SetCharactersVariant | " +
            $"chars=[{ids}] variant={variant} updateCharacters={updateCharacters}");
    }
}
