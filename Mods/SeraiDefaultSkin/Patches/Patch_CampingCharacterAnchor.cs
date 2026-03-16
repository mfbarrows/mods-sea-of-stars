using HarmonyLib;
using UnityEngine;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// Camp/inn scene spawn redirect and log. Separate code path from PlayerParty.
/// No CharacterDefinitionId method parameters — safe to patch.
/// Signature: public CampingCharacterController SpawnCharacter(
///   GameObject campingCharacterPrefab, EPartyCharacterVariant characterVariant = 0)
/// </summary>
[HarmonyPatch(typeof(CampingCharacterAnchor), nameof(CampingCharacterAnchor.SpawnCharacter))]
static class Patch_CampingCharacterAnchor_SpawnCharacter
{
    static void Prefix(CampingCharacterAnchor __instance, UnityEngine.GameObject campingCharacterPrefab, ref EPartyCharacterVariant characterVariant)
    {
        if (characterVariant == EPartyCharacterVariant.ROBOT)
        {
            characterVariant = EPartyCharacterVariant.DEFAULT;
            Plugin.LogI($"[CampingCharacterAnchor] >> SpawnCharacter | " +
                $"charId={__instance.characterDefinitionId.characterId} | " +
                $"variant={EPartyCharacterVariant.ROBOT}->{characterVariant} | " +
                $"prefab={campingCharacterPrefab?.name ?? "null"}");
        } else {
              Plugin.LogD(
                $"[CampingCharacterAnchor] >> SpawnCharacter | " +
                $"charId={__instance.characterDefinitionId.characterId} | " +
                $"variant={characterVariant} | " +
                $"prefab={campingCharacterPrefab?.name ?? "null"}");
        }      
    }
}
