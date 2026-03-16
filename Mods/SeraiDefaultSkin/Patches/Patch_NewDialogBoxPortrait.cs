using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
namespace SeraiDefaultSkin.Patches;

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
    // Custom sprites loaded from disk, keyed by the ROBOT sprite name they replace.
    // Loaded lazily on first SetPortrait call — Sprite.Create requires Unity to be ready.
    static readonly System.Collections.Generic.Dictionary<string, string> s_customSpriteFiles =
        new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["dialog-portrait-Serai-Robot-Determined"] = "custom-portrait-Serai-Determined.png",
            ["dialog-portrait-Serai-Robot-Neutral2"]   = "custom-portrait-Serai-Neutral2.png",
            ["dialog-portrait-Serai-Robot-Surprised"]  = "custom-portrait-Serai-Surprised.png",
            ["dialog-portrait-Serai-Robot-Moved"]      = "custom-portrait-Serai-Moved.png",
        };
    static readonly System.Collections.Generic.Dictionary<string, Sprite?> s_customSprites =
        new System.Collections.Generic.Dictionary<string, Sprite?>(System.StringComparer.Ordinal);

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
            ["dialog-portrait-Serai-Robot-Determined"]   = "d4f6ca4878edd5d4680e5fe0af7d4246", // dialog-portrait-Serai-Determined (fallback)
            ["dialog-portrait-Serai-Robot-Disappointed"] = "a893aaa5dceb7ad4c87e98a42d2cb10e", // dialog-portrait-Serai-Disappointed
            ["dialog-portrait-Serai-Robot-Happy"]        = "7fd499c2e4f29034bad5109752fbd5f3", // dialog-portrait-Serai-Happy
            ["dialog-portrait-Serai-Robot-Neutral"]      = "34cab9ebd85c13b4f84c81ea65da5165", // dialog-portrait-Serai-Neutral
            ["dialog-portrait-Serai-Robot-Neutral2"]     = "34cab9ebd85c13b4f84c81ea65da5165", // dialog-portrait-Serai-Neutral (fallback)
            ["dialog-portrait-Serai-Robot-Sad"]          = "177a7e728d7bcff4c86f38280baec3d3", // dialog-portrait-Serai-Sad
            ["dialog-portrait-Serai-Robot-Shocked"]      = "d1977abaf45251b4a8852e1b00bec8c1", // dialog-portrait-Serai-Shocked
            ["dialog-portrait-Serai-Robot-Surprised"]    = "f8aa1dd1920c074449569047fb9c813f", // dialog-portrait-Serai-Surprised (fallback)
        };

    static Sprite? LoadPortraitFromDisk(string fileName)
    {
        string? dir = System.IO.Path.GetDirectoryName(typeof(Patch_NewDialogBoxPortrait_SetPortrait).Assembly.Location);
        if (dir == null)
        {
            Plugin.LogW($"[NewDialogBoxPortrait] LoadPortraitFromDisk | assembly directory not found");
            return null;
        }
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
        if (s_customSpriteFiles.ContainsKey(originalName))
        {
            bool spriteLoadedAlready = s_customSprites.TryGetValue(originalName, out Sprite? customSprite);
            if (!spriteLoadedAlready)
            {
                // Lazy load — Unity is guaranteed ready by the time SetPortrait fires.
                customSprite = LoadPortraitFromDisk(s_customSpriteFiles[originalName]);
                if (customSprite != null)
                    s_customSprites[originalName] = customSprite;
            }
            if (customSprite != null)
            {
                customSprite.name = originalName;
                portraitSprite = customSprite;
                Plugin.LogI($"[NewDialogBoxPortrait] >> SetPortrait | '{originalName}' -> custom PNG");
                return;
            }
            else
            {
                Plugin.LogW($"[NewDialogBoxPortrait] >> SetPortrait | '{originalName}' custom PNG not loaded");
            }
            
        }

        if (!s_robotToDefaultKey.TryGetValue(originalName, out string? defaultKey))
        {
            // Warn if it looks like a Serai-Robot portrait we haven't mapped — it will show as-is.
            if (originalName.Contains("Serai-Robot-") || originalName.Contains("serai-robot-"))
                Plugin.LogW($"[NewDialogBoxPortrait] >> SetPortrait | '{originalName}' ROBOT portrait NOT in map → showing as-is");
            else 
                Plugin.LogD($"[NewDialogBoxPortrait] >> SetPortrait | '{originalName}'");
            return;
        }

        var handle = Addressables.LoadAssetAsync<Sprite>(defaultKey);
        var defaultSprite = handle.WaitForCompletion();
        if (defaultSprite != null)
        {
            defaultSprite.name = originalName;
            portraitSprite = defaultSprite;
            Plugin.LogI($"[NewDialogBoxPortrait] >> SetPortrait | '{originalName}' -> GUID {defaultKey} (DEFAULT)");
        }
        else
        {
            // GUID load failed — fall back to DEFAULT main portrait rather than leaving ROBOT.
            Plugin.LogW($"[NewDialogBoxPortrait] >> SetPortrait | '{originalName}' GUID load failed, trying GetPortrait fallback");
            var fallback = CharacterDefinitionManager.Instance?.GetPortrait(
                Patch_CharacterDefinitionManager_OnCharacterDefinitionLoaded.SeraiDefault);
            if (fallback != null) { fallback.name = originalName; portraitSprite = fallback; }
            else Plugin.LogW($"[NewDialogBoxPortrait] >> SetPortrait | fallback also unavailable, leaving original");
        }
    }
}
