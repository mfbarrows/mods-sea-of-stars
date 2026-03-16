using HarmonyLib;
using Sabotage.Imposter;
using UnityEngine;
using UnityEngine.AddressableAssets;
namespace SeraiDefaultSkin.Patches;

/// <summary>
/// UI FIX: overwrite ROBOT CharacterDefinition visual fields with DEFAULT's.
/// CharacterDefinitionManager stores a per-variant CharacterDefinition ScriptableObject
/// for each character. All UI sprite lookups resolve to (characterId, currentVariant),
/// where currentVariant is read from the native backing field — get_CurrentVariant()
/// is never called from any C# code, making getter patches completely ineffective.
/// Instead we patch here (class param — no struct marshaling issue) and copy all
/// visual fields from DEFAULT → ROBOT as soon as both ScriptableObjects are loaded.
/// ScriptableObjects are assets that live for the app lifetime, so the copy is permanent
/// and carries across scene reloads.
///
/// For AssetReferenceSprite fields (defaultPortrait, secondaryPortrait): copying the
/// same object reference causes a hang because CharacterDefinitionManager tracks
/// portrait loads by AssetReferenceSprite *instance identity*. Sharing the same
/// instance between ROBOT and DEFAULT causes IsPortraitAlreadyLoading to return true
/// for ROBOT, skipping its load entry while it stays in currentPortraitsToLoads —
/// the list never empties and OnPortraitsToLoadPreloaded never fires.
/// Fix: create a new AssetReferenceSprite(src.AssetGUID) — a distinct object
/// pointing to the same asset GUID. The tracker sees two separate instances and
/// tracks both independently, each loads and completes normally.
///
/// Signature: private void OnCharacterDefinitionLoaded(CharacterDefinition characterDefinition)
/// </summary>
[HarmonyPatch(typeof(CharacterDefinitionManager), "OnCharacterDefinitionLoaded")]
static class Patch_CharacterDefinitionManager_OnCharacterDefinitionLoaded
{
    static CharacterDefinition? s_seraiDefault;
    static CharacterDefinition? s_seraiRobot;

    internal static CharacterDefinition? SeraiDefault => s_seraiDefault;

    static void Postfix(CharacterDefinition characterDefinition)
    {
        if (characterDefinition.characterId != CharacterDefinitionId.Serai.characterId)
            return;

        if (characterDefinition.characterVariant == EPartyCharacterVariant.DEFAULT)
        {
            bool isNew = !System.Object.ReferenceEquals((object?)s_seraiDefault, (object?)characterDefinition);
            s_seraiDefault = characterDefinition;
            Plugin.LogD($"[CharacterDefinitionManager] OnCharacterDefinitionLoaded | Serai DEFAULT {(isNew ? "NEW" : "same-instance")}");
        }
        else if (characterDefinition.characterVariant == EPartyCharacterVariant.ROBOT)
        {
            bool isNew = !System.Object.ReferenceEquals((object?)s_seraiRobot, (object?)characterDefinition);
            s_seraiRobot = characterDefinition;
            Plugin.LogD($"[CharacterDefinitionManager] OnCharacterDefinitionLoaded | Serai ROBOT {(isNew ? "NEW" : "same-instance")}");
        }
        else return;

        // Always copy when both are available — no s_copied guard.
        // CharacterDefinitions are reloaded from Addressables on scene transitions;
        // the old s_copied=true guard silently skipped the re-copy, leaving the
        // freshly-loaded ROBOT CharacterDefinition with its original ROBOT sprites.
        if (s_seraiDefault != null && s_seraiRobot != null)
        {
            Plugin.LogD($"[CharacterDefinitionManager] OnCharacterDefinitionLoaded | CopyVisualFields");
            CopyVisualFields(s_seraiDefault, s_seraiRobot);
        }
    }

    static void CopyVisualFields(CharacterDefinition src, CharacterDefinition dst)
    {
        // Inline Sprite references — already in memory, safe to alias directly.
        dst.miniPortrait          = src.miniPortrait;
        dst.microPortrait         = src.microPortrait;
        dst.microPortraitDisabled = src.microPortraitDisabled;
        dst.combatPortrait        = src.combatPortrait;
        dst.combatPortraitDisabled= src.combatPortraitDisabled;
        dst.mapIcon               = src.mapIcon;
        dst.outOfBoundsPortrait   = src.outOfBoundsPortrait;
        dst.characterColor        = src.characterColor;
        dst.barbershopColor1      = src.barbershopColor1;
        dst.barbershopColor2      = src.barbershopColor2;

        // AssetReferenceSprite fields — must NOT copy the same instance.
        // IsPortraitAlreadyLoading() uses object reference identity as its key.
        // Sharing the same AssetReferenceSprite between DEFAULT and ROBOT means
        // the ROBOT portrait load is skipped ("already loading") while the
        // pending-list entry for ROBOT stays, hanging OnPortraitsToLoadPreloaded.
        // Fix: new AssetReferenceSprite(guid) + SubObjectName — distinct object,
        // same asset and sub-sprite. SubObjectName must be copied too: atlased
        // sprites use it to identify the specific sprite within the atlas; omitting
        // it causes the load to succeed but return blank (white screen).
        if (src.defaultPortrait != null)
        {
            dst.defaultPortrait = new AssetReferenceSprite(src.defaultPortrait.AssetGUID);
            dst.defaultPortrait.SubObjectName = src.defaultPortrait.SubObjectName;
        }
        if (src.secondaryPortrait != null)
        {
            dst.secondaryPortrait = new AssetReferenceSprite(src.secondaryPortrait.AssetGUID);
            dst.secondaryPortrait.SubObjectName = src.secondaryPortrait.SubObjectName;
        }

        // AssetReferenceGameObject — same pattern for previewSprite.
        if (src.previewSprite != null)
        {
            dst.previewSprite = new AssetReferenceGameObject(src.previewSprite.AssetGUID);
            dst.previewSprite.SubObjectName = src.previewSprite.SubObjectName;
        }

        // ImposterAtlasReference — direct alias is safe: atlas loading keys by
        // CharacterDefinitionId (not by reference identity like portrait tracking does).
        // MUST use (object) cast for null checks: ImposterAtlasReference.op_Inequality
        // calls Il2CppObjectBaseToPtrNotNull internally and throws NullReferenceException
        // when the managed wrapper itself is null (not an IL2CPP null).
        var srcAtlas = (object?)src.requiredImposterAtlas;
        var dstAtlas = (object?)dst.requiredImposterAtlas;
        bool atlasAlreadySame = System.Object.ReferenceEquals(srcAtlas, dstAtlas);
        Plugin.LogD($"  requiredImposterAtlas: src.hash={srcAtlas?.GetHashCode() ?? -1}"
            + $" dst.hash={dstAtlas?.GetHashCode() ?? -1}"
            + $" alreadySame={atlasAlreadySame}");
        if (srcAtlas != null)
            dst.requiredImposterAtlas = src.requiredImposterAtlas;

        // Summary: confirm key fields were actually set to DEFAULT values.
        Plugin.LogD($"  combatPortrait='{dst.combatPortrait?.name ?? "null"}' "
            + $"miniPortrait='{dst.miniPortrait?.name ?? "null"}' "
            + $"defaultPortrait.GUID='{dst.defaultPortrait?.AssetGUID ?? "null"}'");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CharacterDefinitionManager — portrait preload diagnostics
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// PreloadPortrait(CharacterDefinition) — public, fires when a character
/// portrait is about to be loaded from Addressables. Logs which variant's
/// portrait is being preloaded so we can confirm both DEFAULT and ROBOT Serai
/// portraits are requested (and that copy timing doesn't cause a race).
/// Signature: public void PreloadPortrait(CharacterDefinition characterDefinition)
/// </summary>
[HarmonyPatch]
static class Patch_CharacterDefinitionManager_PreloadPortrait
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(CharacterDefinitionManager))
            .Find(m => m.Name == "PreloadPortrait"
                    && m.IsPublic
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType.Name == "CharacterDefinition");

    static void Prefix(CharacterDefinition characterDefinition)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[CharacterDefinitionManager] >> PreloadPortrait | "
            + $"charId={characterDefinition?.characterId ?? "null"} "
            + $"variant={characterDefinition?.characterVariant} "
            + $"defaultPortrait.GUID={characterDefinition?.defaultPortrait?.AssetGUID ?? "null"}");
    }
}

/// <summary>
/// GetPortrait(CharacterDefinition) — public, fires when a portrait sprite is
/// retrieved for display. Logs which CharacterDefinition is being read so we
/// can confirm the ROBOT definition's portrait was overwritten with DEFAULT's
/// before the call. The CharacterDefinitionId-overload (struct param) is NOT
/// patchable; only this class-param overload is interceptable.
/// Signature: public Sprite GetPortrait(CharacterDefinition characterDefinition)
/// </summary>
[HarmonyPatch]
static class Patch_CharacterDefinitionManager_GetPortrait
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(CharacterDefinitionManager))
            .Find(m => m.Name == "GetPortrait"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType.Name == "CharacterDefinition");

    static void Postfix(CharacterDefinition characterDefinition, Sprite __result)
    {
        if (!Diag.Enabled) return;
        if (characterDefinition?.characterId != CharacterDefinitionId.Serai.characterId) return;
        Plugin.LogD($"[CharacterDefinitionManager] << GetPortrait | "
            + $"variant={characterDefinition.characterVariant} "
            + $"result='{__result?.name ?? "null"}'");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CharacterDefinitionManager.GetVariantAnimationClipOverride — FIX
//
// This method maps DEFAULT animation clips → variant-specific clips based on
// CharacterVariantAnimClipOverride ScriptableObjects registered per character.
// For SERAI ROBOT, it would return ROBOT clips that reference sprites outside
// the DEFAULT 'Serai' ImposterAtlas. CharacterVisual.LateUpdate then fails to
// look up those sprites → falls back to raw SpriteRenderer → ROBOT sprites show.
//
// Fix: when charId=SERAI and variant=ROBOT, return the original clip unchanged,
// effectively suppressing all ROBOT animation overrides for Serai.
// Log every call for Serai so we can confirm coverage.
// ─────────────────────────────────────────────────────────────────────────────
[HarmonyPatch(typeof(CharacterDefinitionManager),
    nameof(CharacterDefinitionManager.GetVariantAnimationClipOverride))]
static class Patch_CharacterDefinitionManager_GetVariantAnimationClipOverride
{
    static void Postfix(
        CharacterDefinitionId characterDefinitionId,
        EPartyCharacterVariant partyCharacterVariant,
        AnimationClip animationClip,
        ref AnimationClip __result)
    {
        // characterDefinitionId is an IL2CPP struct whose characterId string
        // pointer can be null when called early (e.g. from CampingCharacterAnchor
        // before character definitions are fully loaded). Guard the whole body.
        string charId;
        try { charId = characterDefinitionId.characterId; }
        catch { return; }

        if (charId != CharacterDefinitionId.Serai.characterId) return;

        bool overrideApplied = (object?)__result != null
            && !System.Object.ReferenceEquals((object?)__result, (object?)animationClip);

        if (partyCharacterVariant == EPartyCharacterVariant.ROBOT && overrideApplied)
        {
            Plugin.LogI($"[CharacterDefinitionManager] GetVariantAnimationClipOverride | "
                + $"SERAI ROBOT: suppressing override '{__result?.name}' <- keeping '{animationClip?.name}'");
            __result = animationClip;
        }
        else if (Diag.Enabled)
        {
            Plugin.LogD($"[CharacterDefinitionManager] GetVariantAnimationClipOverride | "
                + $"SERAI variant={partyCharacterVariant} orig='{animationClip?.name}' result='{__result?.name}' overrideApplied={overrideApplied}");
        }
    }
}
