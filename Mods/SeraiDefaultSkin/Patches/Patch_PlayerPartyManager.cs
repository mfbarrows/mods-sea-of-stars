using HarmonyLib;
using UnityEngine;
namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// PlayerPartyManager — diagnostic patches for all patchable methods
// Not patchable (CharacterDefinitionId struct first param):
//   AddPartyMember, RemovePartyMember, ShelvePartyMember, AddToCombatParty,
//   RemoveFromCombatParty, SwapCombatPartyMember, GetPartyCharacter,
//   GetCombatActor, IsInCombatParty, IsLeaderOrFollower, SetLeader,
//   SetMainCharacter, IsCharacterInFullParty, RemoveFromCurrentParty,
//   SwapCharacterGameObject, SwapCharacterOrderInParty, GetGuestPartyCharacter.
// Not patchable (PlayerPartyLoader struct first param):
//   OnPartyLoadingDone, OnVariantUpdated.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// DIAGNOSTIC: fires at the end of every SwapCharacterGameObject call (and on
/// normal party load completion). Dumps all live PlayerPartyCharacter names so
/// we can confirm whether a ROBOT instance entered the party list during the
/// cutscene.
/// Signature: public void ApplyRenderingSettingsToParty()
/// </summary>
[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.ApplyRenderingSettingsToParty))]
static class Patch_PlayerPartyManager_ApplyRenderingSettingsToParty
{
    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> ApplyRenderingSettingsToParty");
    }

    static void Postfix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] << ApplyRenderingSettingsToParty");
        var chars = __instance.CurrentPartyCharacters;
        if (chars == null || chars.Count == 0)
        {
            Plugin.LogD($"[PlayerPartyManager] << ApplyRenderingSettingsToParty | party=[]");
            return;
        }
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        foreach (var c in chars)
        {
            if (c == null) { sb.Append("<null>, "); continue; }
            var goName = c.gameObject?.name ?? "<no go>";
            // CharacterDefinitionId is public on the base Character class
            var charId = c.characterDefinitionId.characterId;
            // Look up PlayableCharacterData to read current variant and class
            EPartyCharacterVariant variant = EPartyCharacterVariant.DEFAULT;
            string className = "?";
            try
            {
                var statsData = Manager<CharacterStatsManager>.Instance?.GetCharacterData(c.characterDefinitionId);
                if (statsData != null)
                {
                    variant = statsData.CurrentVariant;
                    className = statsData.CurrentClass?.GetType().Name ?? "null";
                }
            }
            catch { }
            sb.Append($"{goName}(id={charId},variant={variant},class={className}), ");
        }
        sb.Append(']');
        Plugin.LogD($"[PlayerPartyManager] << ApplyRenderingSettingsToParty | party={sb}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), "Initialize")]
static class Patch_PlayerPartyManager_Initialize
{
    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> Initialize | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.OverridePartyMembersToLoad))]
static class Patch_PlayerPartyManager_OverridePartyMembersToLoad
{
    static void Prefix(Il2CppSystem.Collections.Generic.List<CharacterDefinitionId> toLoad)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> OverridePartyMembersToLoad | count={toLoad?.Count}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.StopOverridePartyMembersToLoad))]
static class Patch_PlayerPartyManager_StopOverridePartyMembersToLoad
{
    static void Prefix()
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> StopOverridePartyMembersToLoad");
    }
}

/// <summary>
/// LoadParty(Action&lt;PlayerParty&gt;) — the 1-param public façade.
/// Called by scene-load triggers and BT nodes when no specific prefab is given.
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_LoadParty_1
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerPartyManager))
            .Find(m => m.Name == "LoadParty" && m.GetParameters().Length == 1);

    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> LoadParty(callback) | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// LoadParty(GameObject, Action&lt;PlayerParty&gt;, bool) — 3-param overload.
/// Called when a specific party prefab is given (e.g. after SwapParty).
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_LoadParty_3
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerPartyManager))
            .Find(m => m.Name == "LoadParty" && m.GetParameters().Length == 3
                    && m.GetParameters()[0].ParameterType.Name == "GameObject");

    static void Prefix(PlayerPartyManager __instance, GameObject partyPrefab, bool additionalParty)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> LoadParty(prefab,callback,additional) | " +
            $"prefab={partyPrefab?.name ?? "null"} additionalParty={additionalParty} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// LoadParty(GameObject, Action&lt;PlayerParty&gt;, bool, Vector3) — 4-param overload.
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_LoadParty_4
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerPartyManager))
            .Find(m => m.Name == "LoadParty" && m.GetParameters().Length == 4
                    && m.GetParameters()[0].ParameterType.Name == "GameObject");

    static void Prefix(PlayerPartyManager __instance, GameObject partyPrefab, bool additionalParty, Vector3 loadPosition)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> LoadParty(prefab,callback,additional,pos) | " +
            $"prefab={partyPrefab?.name ?? "null"} additionalParty={additionalParty} pos={loadPosition} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// AddLoadedParty — private, fires when a freshly-loaded PlayerParty instance
/// is registered with the manager (right before SetupParty).
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_AddLoadedParty
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.Method(typeof(PlayerPartyManager), "AddLoadedParty");

    static void Prefix(PlayerPartyManager __instance, GameObject partyPrefab, PlayerParty playerParty)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> AddLoadedParty | " +
            $"prefab={partyPrefab?.name ?? "null"} party={playerParty?.GetHashCode():X} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// OnPartyDestroyed — private, fires when a PlayerParty GO is destroyed
/// (scene transition, SwapParty old party teardown, etc.).
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_OnPartyDestroyed
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.Method(typeof(PlayerPartyManager), "OnPartyDestroyed");

    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> OnPartyDestroyed | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// OnCharacterVariantChanged — the PlayerPartyManager-level handler.
/// Called via CharacterStatsManager.SetCharactersVariant when a BT variant
/// node fires. List&lt;CharacterDefinitionId&gt; first param = patchable.
/// </summary>
[HarmonyPatch(typeof(PlayerPartyManager), "OnCharacterVariantChanged")]
static class Patch_PlayerPartyManager_OnCharacterVariantChanged
{
    static void Prefix(PlayerPartyManager __instance, Il2CppSystem.Collections.Generic.List<CharacterDefinitionId> characters,
        EPartyCharacterVariant variant, bool updateCharacters)
    {
        if (!Diag.Enabled) return;
        var sb = new System.Text.StringBuilder();
        if (characters != null) for (int i = 0; i < characters.Count; i++) sb.Append(characters[i].characterId).Append(',');
        Plugin.LogD($"[PlayerPartyManager] >> OnCharacterVariantChanged | " +
            $"chars=[{sb}] variant={variant} updateCharacters={updateCharacters} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// SwapParty(GameObject, Action, bool) — replaces the whole party prefab in-scene.
/// Fires during scene transitions and some cutscenes.
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_SwapParty_3
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerPartyManager))
            .Find(m => m.Name == "SwapParty" && m.GetParameters().Length == 3);

    static void Prefix(PlayerPartyManager __instance, GameObject partyPrefab, bool activateNewParty)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> SwapParty(prefab,callback,activate) | " +
            $"prefab={partyPrefab?.name ?? "null"} activate={activateNewParty} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// SwapParty(GameObject, Action, Vector3, bool) — 4-param overload with explicit position.
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_SwapParty_4
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerPartyManager))
            .Find(m => m.Name == "SwapParty" && m.GetParameters().Length == 4);

    static void Prefix(PlayerPartyManager __instance, GameObject partyPrefab, Vector3 newPartyPosition, bool activateNewParty)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> SwapParty(prefab,callback,pos,activate) | " +
            $"prefab={partyPrefab?.name ?? "null"} pos={newPartyPosition} activate={activateNewParty} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// SetupParty — private, assembles the live leader/follower/combat lists after
/// a party load or swap. Primary assembly point for CurrentPartyCharacters.
/// </summary>
[HarmonyPatch]
static class Patch_PlayerPartyManager_SetupParty
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.Method(typeof(PlayerPartyManager), "SetupParty");

    static void Prefix(PlayerPartyManager __instance, bool activateParty)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> SetupParty | activateParty={activateParty} | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.SetCurrentParty))]
static class Patch_PlayerPartyManager_SetCurrentParty
{
    static void Prefix(Il2CppSystem.Collections.Generic.List<CharacterDefinitionId> currentParty)
    {
        if (!Diag.Enabled) return;
        var sb = new System.Text.StringBuilder();
        if (currentParty != null) for (int i = 0; i < currentParty.Count; i++) sb.Append(currentParty[i].characterId).Append(',');
        Plugin.LogD($"[PlayerPartyManager] >> SetCurrentParty | party=[{sb}]");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.SetGuestPartyMembers))]
static class Patch_PlayerPartyManager_SetGuestPartyMembers
{
    static void Prefix(Il2CppSystem.Collections.Generic.List<CharacterDefinitionId> guestPartyMembers)
    {
        if (!Diag.Enabled) return;
        var sb = new System.Text.StringBuilder();
        if (guestPartyMembers != null) for (int i = 0; i < guestPartyMembers.Count; i++) sb.Append(guestPartyMembers[i].characterId).Append(',');
        Plugin.LogD($"[PlayerPartyManager] >> SetGuestPartyMembers | guests=[{sb}]");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.StoreCurrentParty))]
static class Patch_PlayerPartyManager_StoreCurrentParty
{
    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> StoreCurrentParty | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.SwapPartyLeader))]
static class Patch_PlayerPartyManager_SwapPartyLeader
{
    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> SwapPartyLeader | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.UpdateCombatPartyCharacters))]
static class Patch_PlayerPartyManager_UpdateCombatPartyCharacters
{
    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> UpdateCombatPartyCharacters | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch]
static class Patch_PlayerPartyManager_SetCombatParty_Characters
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerPartyManager))
            .Find(m => m.Name == "SetCombatParty"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType.IsGenericType
                    && m.GetParameters()[0].ParameterType.GetGenericArguments()[0].Name == "PlayerPartyCharacter");

    static void Prefix(Il2CppSystem.Collections.Generic.List<PlayerPartyCharacter> newCombatPartyCharacters)
    {
        if (!Diag.Enabled) return;
        var sb = new System.Text.StringBuilder();
        if (newCombatPartyCharacters != null)
            for (int i = 0; i < newCombatPartyCharacters.Count; i++)
                sb.Append(newCombatPartyCharacters[i]?.gameObject?.name ?? "null").Append(',');
        Plugin.LogD($"[PlayerPartyManager] >> SetCombatParty(PPC list) | party=[{(newCombatPartyCharacters == null ? "null" : sb.ToString())}]");
    }
}

[HarmonyPatch]
static class Patch_PlayerPartyManager_SetCombatParty_Ids
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerPartyManager))
            .Find(m => m.Name == "SetCombatParty"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType.IsGenericType
                    && m.GetParameters()[0].ParameterType.GetGenericArguments()[0].Name == "CharacterDefinitionId");

    static void Prefix(Il2CppSystem.Collections.Generic.List<CharacterDefinitionId> combatParty)
    {
        if (!Diag.Enabled) return;
        var sb = new System.Text.StringBuilder();
        if (combatParty != null) for (int i = 0; i < combatParty.Count; i++) sb.Append(combatParty[i].characterId).Append(',');
        Plugin.LogD($"[PlayerPartyManager] >> SetCombatParty(id list) | party=[{sb}]");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.RegisterFollowers))]
static class Patch_PlayerPartyManager_RegisterFollowers
{
    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> RegisterFollowers | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.ClearFollowers))]
static class Patch_PlayerPartyManager_ClearFollowers
{
    static void Prefix(PlayerPartyManager __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> ClearFollowers | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.ShowOnlyLeaderAndFollowers))]
static class Patch_PlayerPartyManager_ShowOnlyLeaderAndFollowers
{
    static void Prefix(PlayerPartyManager __instance, bool applyOnlyToCurrentParty)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> ShowOnlyLeaderAndFollowers | applyOnlyToCurrentParty={applyOnlyToCurrentParty} | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.SetPartyConfig))]
static class Patch_PlayerPartyManager_SetPartyConfig
{
    static void Prefix(PlayerPartyManager __instance, StoredParty partyConfig, bool unEquipPartyMembers)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> SetPartyConfig | unEquipPartyMembers={unEquipPartyMembers} | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.ChangeLineup))]
static class Patch_PlayerPartyManager_ChangeLineup
{
    static void Prefix(PlayerPartyManager __instance, EPartyLineupContext newLineup, bool copyPreviousInMemory)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> ChangeLineup | lineup={newLineup} copyPrevious={copyPreviousInMemory} | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.ClearPartyMembers))]
static class Patch_PlayerPartyManager_ClearPartyMembers
{
    static void Prefix(PlayerPartyManager __instance, bool unEquipPartyMembers)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> ClearPartyMembers | unEquipPartyMembers={unEquipPartyMembers} | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.GroundParty))]
static class Patch_PlayerPartyManager_GroundParty
{
    static void Prefix(PlayerPartyManager __instance, bool assignCollisionInfo)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> GroundParty | assignCollisionInfo={assignCollisionInfo} | instance={__instance.GetHashCode():X}");
    }
}

[HarmonyPatch(typeof(PlayerPartyManager), nameof(PlayerPartyManager.SetPartyOrientation))]
static class Patch_PlayerPartyManager_SetPartyOrientation
{
    static void Prefix(PlayerPartyManager __instance, int playerOrientation)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerPartyManager] >> SetPartyOrientation | orientation={playerOrientation} | instance={__instance.GetHashCode():X}");
    }
}
