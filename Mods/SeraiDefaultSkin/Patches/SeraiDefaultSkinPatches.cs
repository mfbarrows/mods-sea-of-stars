using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// SeraiDefaultSkin patches
//
// Strategy: mutate seraiVariantsPrefabs[robotIdx] → default prefab BEFORE
// LoadPartyCharacter calls GetCharacterVariantReference, so the Addressable
// that gets enqueued is already the DEFAULT one. Save/field/move-set untouched.
//
// Instance pattern (confirmed from logs):
//   Two PlayerParty instances exist per scene load:
//   1. Scene blueprint  — instantiatedParty=False, Start() fires first.
//      Has serialized prefab lists but is NOT used for actual loading.
//      Destroyed by its own Start() (stores scene+position to manager, self-destructs).
//   2. Runtime party    — instantiatedParty=True, Instantiate()d from the prefab.
//      LoadPartyCharacter → GetCharacterVariantReference → Addressable load →
//      OnPartyLoaded fires THEN Start() fires on this instance.
//      Swap in Start() Postfix is ~25ms too late on this instance.
//
// Primary fix: Patch LoadParty(PartyLoadingData) Prefix — fires on the runtime
//   instance just before the LoadPartyCharacter loop. PartyLoadingData is a
//   class (no struct), so the Harmony trampoline is safe.
//
// UI fix: Patch CharacterDefinitionManager.OnCharacterDefinitionLoaded Postfix.
//   Every UI sprite (portrait, combatPortrait, mapIcon, previewSprite, etc.)
//   resolves to a per-variant CharacterDefinition ScriptableObject. get_CurrentVariant
//   is NEVER called by any C# code — every caller reads the backing field at 0x4C
//   via native IL2CPP field access, making getter patches completely ineffective.
//   Instead: overwrite the ROBOT ScriptableObject's visual fields with DEFAULT's
//   values as soon as both are loaded. All future lookups for (Serai, ROBOT) return
//   visual data that looks identical to (Serai, DEFAULT).
//
// LoadPartyCharacter(CharacterDefinitionId, EPartyCharacterVariant) — NOT
//   patched for redirect: CharacterDefinitionId struct param prevents the
//   Harmony trampoline from applying (patch silently does not fire).
//
// NOT patched:
//   - GetCharacterVariantReference: hangs save loading even as a no-op.
//   - SetVariant / get_CurrentVariant: native code reads the backing field
//     directly; property getter patches are bypassed for all callers.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// PRIMARY SWAP: fires on the runtime party instance (instantiatedParty=True)
/// just before the LoadPartyCharacter loop begins. Swaps seraiVariantsPrefabs
/// so GetCharacterVariantReference returns the DEFAULT Addressable for ROBOT.
/// Uses TargetMethod() to target the private overload — PartyLoadingData is a
/// class, so no struct marshaling issue.
/// Signature: private void LoadParty(PlayerParty.PartyLoadingData loadData)
/// </summary>
[HarmonyPatch]
static class Patch_PlayerParty_LoadParty_PartyLoadingData
{
    static System.Reflection.MethodBase TargetMethod()
    {
        var dataType = AccessTools.Inner(typeof(PlayerParty), "PartyLoadingData");
        return AccessTools.Method(typeof(PlayerParty), "LoadParty", new[] { dataType });
    }

    static void Prefix(PlayerParty __instance)
    {
        var variants = __instance.seraiVariants;
        var prefabs  = __instance.seraiVariantsPrefabs;

        if (variants == null || prefabs == null)
        {
            Plugin.LogW($"[PlayerParty] >> LoadParty(data) | lists null | instance={__instance.GetHashCode():X}");
            return;
        }

        int defaultIdx = -1, robotIdx = -1;
        for (int i = 0; i < variants.Count; i++)
        {
            if (variants[i] == EPartyCharacterVariant.DEFAULT) defaultIdx = i;
            if (variants[i] == EPartyCharacterVariant.ROBOT)   robotIdx   = i;
        }

        if (defaultIdx < 0 || robotIdx < 0)
        {
            Plugin.LogW($"[PlayerParty] >> LoadParty(data) | variant indices not found - DEFAULT:{defaultIdx} ROBOT:{robotIdx} | instance={__instance.GetHashCode():X}");
            return;
        }

        prefabs[robotIdx] = prefabs[defaultIdx];
        Plugin.LogI($"[PlayerParty] >> LoadParty(data) | seraiVariantsPrefabs[{robotIdx}](ROBOT) -> DEFAULT | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// BELT+SUSPENDERS: redundant swap kept in case the prefab lists are
/// re-initialised between LoadParty(data) and Start() on the runtime instance.
/// Harmless if lists are already swapped (assigns same value).
/// Also fires on the scene blueprint instance (instantiatedParty=False) —
/// that object is destroyed immediately after, so the swap there does nothing.
/// Signature: private void Start()
/// </summary>
[HarmonyPatch(typeof(PlayerParty), "Start")]
static class Patch_PlayerParty_Start
{
    // // Prefix fires BEFORE Start() stores scene/position and destroys itself.
    // // Logs instance identity so we can compare with LoadPartyCharacter logs.
    // static void Prefix(PlayerParty __instance)
    // {
    //     Plugin.LogD($"[PlayerParty] >> Start (enter) | " +
    //         $"instantiatedParty={__instance.instantiatedParty} | " +
    //         $"instance={__instance.GetHashCode():X}");
    // }

    // Postfix fires AFTER Start() — note: the gameObject is already destroyed
    // (or will be destroyed end-of-frame) on the normal path.
    static void Postfix(PlayerParty __instance)
    {
        var variants = __instance.seraiVariants;
        var prefabs  = __instance.seraiVariantsPrefabs;

        if (variants == null || prefabs == null)
        {
            Plugin.LogW($"[PlayerParty] << Start (exit) | lists null | instance={__instance.GetHashCode():X}");
            return;
        }

        int defaultIdx = -1, robotIdx = -1;
        for (int i = 0; i < variants.Count; i++)
        {
            if (variants[i] == EPartyCharacterVariant.DEFAULT) defaultIdx = i;
            if (variants[i] == EPartyCharacterVariant.ROBOT)   robotIdx   = i;
        }

        if (defaultIdx < 0 || robotIdx < 0)
        {
            Plugin.LogW($"[PlayerParty] << Start (exit) | variant indices not found - DEFAULT:{defaultIdx} ROBOT:{robotIdx} (count:{variants.Count}) | instance={__instance.GetHashCode():X}");
            return;
        }

        prefabs[robotIdx] = prefabs[defaultIdx];
        Plugin.LogI($"[PlayerParty] << Start (exit) | seraiVariantsPrefabs[{robotIdx}](ROBOT) -> DEFAULT | instance={__instance.GetHashCode():X}");
    }
}

// /// <summary>
// /// DIAGNOSTIC: expected to be SILENT — CharacterDefinitionId struct as first
// /// param prevents the Harmony trampoline from applying in IL2CPP. Kept so we
// /// notice if it starts firing (would mean the struct issue is resolved).
// /// Signature: private void LoadPartyCharacter(CharacterDefinitionId character,
// ///   EPartyCharacterVariant characterVariant)
// /// </summary>
// [HarmonyPatch(typeof(PlayerParty), "LoadPartyCharacter")]
// static class Patch_PlayerParty_LoadPartyCharacter
// {
//     static void Prefix(PlayerParty __instance, EPartyCharacterVariant characterVariant)
//     {
//         Plugin.LogD($"[PlayerParty] >> LoadPartyCharacter | variant={characterVariant} | instance={__instance.GetHashCode():X}");
//     }
// }

// /// <summary>
// /// Log-only: fires when all party Addressables have finished loading.
// /// If this appears before Start() in the log, character loading is complete
// /// before Start() runs — confirming the swap in Start() is too late.
// /// Signature: private void OnPartyLoaded()
// /// </summary>
// [HarmonyPatch(typeof(PlayerParty), "OnPartyLoaded")]
// static class Patch_PlayerParty_OnPartyLoaded
// {
//     static void Prefix(PlayerParty __instance)
//     {
//         Plugin.LogD($"[PlayerParty] >> OnPartyLoaded | instantiatedParty={__instance.instantiatedParty} | instance={__instance.GetHashCode():X}");
//     }
// }

/// <summary>
/// UI FIX: overwrite ROBOT CharacterDefinition visual fields with DEFAULT's.
/// CharacterDefinitionManager stores a per-variant CharacterDefinition ScriptableObject
/// for each character. All UI sprite lookups resolve to (characterId, currentVariant),
/// where currentVariant is read from the native backing field — get_CurrentVariant()
/// is never called from any C# code, making getter patches ineffective.
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
    static CharacterDefinition s_seraiDefault;
    static CharacterDefinition s_seraiRobot;

    internal static CharacterDefinition SeraiDefault => s_seraiDefault;

    static void Postfix(CharacterDefinition characterDefinition)
    {
        if (characterDefinition.characterId != CharacterDefinitionId.Serai.characterId)
            return;

        if (characterDefinition.characterVariant == EPartyCharacterVariant.DEFAULT)
        {
            s_seraiDefault = characterDefinition;
            Plugin.LogD($"[CharacterDefinitionManager] OnCharacterDefinitionLoaded | Serai DEFAULT captured");
        }
        else if (characterDefinition.characterVariant == EPartyCharacterVariant.ROBOT)
        {
            s_seraiRobot = characterDefinition;
            Plugin.LogD($"[CharacterDefinitionManager] OnCharacterDefinitionLoaded | Serai ROBOT captured");
        }

        if (s_seraiDefault != null && s_seraiRobot != null)
        {
            CopyVisualFields(s_seraiDefault, s_seraiRobot);
            Plugin.LogI($"[CharacterDefinitionManager] OnCharacterDefinitionLoaded | Serai ROBOT visual fields <- DEFAULT");
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
    }
}

// /// <summary>
// /// Debug log: records every C#-side read of Serai's current variant.
// /// Confirmed: this is NEVER called — all callers read the backing field at 0x4C
// /// directly via native IL2CPP field access. Kept as a canary; if it ever fires
// /// it means something changed and the getter is now reachable from C# code.
// /// Signature: public EPartyCharacterVariant get_CurrentVariant()
// /// </summary>
// [HarmonyPatch(typeof(PlayableCharacterData), nameof(PlayableCharacterData.CurrentVariant), MethodType.Getter)]
// static class Patch_PlayableCharacterData_get_CurrentVariant
// {
//     static void Postfix(PlayableCharacterData __instance, EPartyCharacterVariant __result)
//     {
//         if (__instance.characterId == CharacterDefinitionId.Serai)
//             Plugin.LogD($"[PlayableCharacterData] >> get_CurrentVariant | result={__result}");
//     }
// }

// /// <summary>
// /// Debug log: records every SetVariant call for Serai — variant written,
// /// reloadMoveSet flag, and load flag.
// /// Signature: public void SetVariant(EPartyCharacterVariant variant,
// ///   bool reloadMoveSet = false, bool load = true)
// /// </summary>
// [HarmonyPatch(typeof(PlayableCharacterData), nameof(PlayableCharacterData.SetVariant))]
// static class Patch_PlayableCharacterData_SetVariant
// {
//     static void Prefix(PlayableCharacterData __instance, EPartyCharacterVariant variant, bool reloadMoveSet, bool load)
//     {
//         Plugin.LogD($"[PlayableCharacterData] >> SetVariant | " +
//             $"prev={__instance.CurrentVariant} → next={variant} | " +
//             $"reloadMoveSet={reloadMoveSet} load={load}");
//     }
// }

/// <summary>
/// CUTSCENE PORTRAIT FIX: PlayDialogNode has a customPortrait: GraphVariable&lt;Sprite&gt;
/// field that some cutscene dialogue nodes use to hardcode a specific portrait
/// sprite at design time. This sprite is loaded directly from serialised
/// cutscene data and bypasses CharacterDefinitionManager — so even after our
/// CharacterDefinition field copy, ROBOT's portrait sprite can slip through here.
///
/// Fix: intercept SetPortrait, identify ROBOT portrait Sprites by name using an
/// explicit table (derived from Addressables catalog — see
/// Docs/serai-dialog-portrait-catalog-keys.txt), load the matching DEFAULT
/// portrait by Addressable key, and swap. ROBOT expressions that have no
/// DEFAULT equivalent (Moved, GoldenPelican) are absent from the table and
/// are left unchanged.
///
/// Signature: public void SetPortrait(Sprite portraitSprite, Sprite background)
/// </summary>
[HarmonyPatch(typeof(NewDialogBoxPortrait), "SetPortrait")]
static class Patch_NewDialogBoxPortrait_SetPortrait
{
    // Maps ROBOT portrait sprite names → DEFAULT asset GUIDs.
    // These GUIDs are from m_KeyDataString in the Addressables catalog
    // (SeaOfStars_Data/StreamingAssets/aa/catalog.json). String names like
    // "dialog-portrait-Serai-Determined" are NOT registered address keys —
    // they only appear as substrings of m_InternalIds asset paths. Only GUIDs
    // (and full asset paths) are valid loadable keys for these assets.
    // Neutral2 has no DEFAULT equivalent so it falls back to Neutral.
    // Moved and GoldenPelican are absent — those portraits are left unchanged.
    static readonly System.Collections.Generic.Dictionary<string, string> s_robotToDefaultKey =
        new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["dialog-portrait-Serai-Robot-Default"]      = "53e7c87d08204ba4487b6f3a85ba9480", // dialog-portrait-Serai
            ["dialog-portrait-Serai-Robot-Determined"]   = "d4f6ca4878edd5d4680e5fe0af7d4246", // dialog-portrait-Serai-Determined
            ["dialog-portrait-Serai-Robot-Disappointed"] = "a893aaa5dceb7ad4c87e98a42d2cb10e", // dialog-portrait-Serai-Disappointed
            ["dialog-portrait-Serai-Robot-Happy"]        = "7fd499c2e4f29034bad5109752fbd5f3", // dialog-portrait-Serai-Happy
            ["dialog-portrait-Serai-Robot-Neutral"]      = "34cab9ebd85c13b4f84c81ea65da5165", // dialog-portrait-Serai-Neutral
            ["dialog-portrait-Serai-Robot-Neutral2"]     = "34cab9ebd85c13b4f84c81ea65da5165", // dialog-portrait-Serai-Neutral (fallback)
            ["dialog-portrait-Serai-Robot-Sad"]          = "177a7e728d7bcff4c86f38280baec3d3", // dialog-portrait-Serai-Sad
            ["dialog-portrait-Serai-Robot-Shocked"]      = "d1977abaf45251b4a8852e1b00bec8c1", // dialog-portrait-Serai-Shocked
            ["dialog-portrait-Serai-Robot-Surprised"]    = "f8aa1dd1920c074449569047fb9c813f", // dialog-portrait-Serai-Surprised
        };

    // Custom sprites loaded from disk, keyed by the ROBOT sprite name they replace.
    static readonly System.Collections.Generic.Dictionary<string, Sprite> s_customSprites =
        new System.Collections.Generic.Dictionary<string, Sprite>(System.StringComparer.Ordinal)
        {
            ["dialog-portrait-Serai-Robot-Determined"] = LoadPortraitFromDisk("custom-portrait-Serai-Determined.png"),
            ["dialog-portrait-Serai-Robot-Neutral2"] = LoadPortraitFromDisk("custom-portrait-Serai-Neutral2.png"),
            ["dialog-portrait-Serai-Robot-Surprised"] = LoadPortraitFromDisk("custom-portrait-Serai-Surprised.png"),
            ["dialog-portrait-Serai-Robot-Moved"] = LoadPortraitFromDisk("custom-portrait-Serai-Moved.png"),
        };

    static Sprite LoadPortraitFromDisk(string fileName)
    {
        var dir = System.IO.Path.GetDirectoryName(typeof(Patch_NewDialogBoxPortrait_SetPortrait).Assembly.Location);
        var path = System.IO.Path.Combine(dir, "portraits", fileName);
        if (!System.IO.File.Exists(path))
        {
            Plugin.LogW($"[NewDialogBoxPortrait] LoadPortraitFromDisk | file not found: {path}");
            return null;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        UnityEngine.ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path));
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        Plugin.LogI($"[NewDialogBoxPortrait] LoadPortraitFromDisk | loaded '{fileName}' ({tex.width}x{tex.height})");
        return sprite;
    }

    static void Prefix(ref Sprite portraitSprite)
    {
        if (portraitSprite == null) return;
        var originalName = portraitSprite.name;

        // Check custom disk-loaded sprites first.
        if (s_customSprites.TryGetValue(originalName, out var customSprite))
        {
            if (customSprite != null)
            {
                customSprite.name = originalName;
                portraitSprite = customSprite;
                Plugin.LogI($"[NewDialogBoxPortrait] SetPortrait | '{originalName}' -> custom PNG");
                return;
            }
            else
            {
                Plugin.LogW($"[NewDialogBoxPortrait] SetPortrait | '{originalName}' custom PNG not loaded, leaving original");
            }
            
        }

        if (!s_robotToDefaultKey.TryGetValue(originalName, out string? defaultKey)) return;

        var handle = Addressables.LoadAssetAsync<Sprite>(defaultKey);
        var defaultSprite = handle.WaitForCompletion();
        if (defaultSprite != null)
        {
            defaultSprite.name = originalName;
            portraitSprite = defaultSprite;
            Plugin.LogI($"[NewDialogBoxPortrait] SetPortrait | '{originalName}' -> GUID {defaultKey} (DEFAULT)");
        }
        else
        {
            // GUID load failed — fall back to DEFAULT main portrait rather than leaving ROBOT.
            Plugin.LogW($"[NewDialogBoxPortrait] SetPortrait | '{originalName}' GUID load failed, trying GetPortrait fallback");
            var fallback = CharacterDefinitionManager.Instance?.GetPortrait(
                Patch_CharacterDefinitionManager_OnCharacterDefinitionLoaded.SeraiDefault);
            if (fallback != null) { fallback.name = originalName; portraitSprite = fallback; }
            else Plugin.LogW($"[NewDialogBoxPortrait] SetPortrait | fallback also unavailable, leaving original");
        }
    }
}

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
