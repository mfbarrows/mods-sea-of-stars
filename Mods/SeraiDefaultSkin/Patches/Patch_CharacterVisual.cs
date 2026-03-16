using HarmonyLib;
using Sabotage.Imposter;
using UnityEngine;
namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// CharacterVisual.Initialize — log which sprite sheet (ImposterAtlas) is bound
// to each character GO, and which sprite is active at that moment.
//
// CharacterVisual is the MonoBehaviour on a character's root GO that drives all
// 2D imposter rendering: it holds the ImposterAtlas (the sprite sheet) and the
// SpriteRenderer. Initialize() fires whenever the atlas is (re)assigned.
//
// ImposterAtlas.atlasName  — the human-readable name authored by artists.
// ImposterAtlas.name       — the ScriptableObject asset name (fallback).
// SpriteRenderer.sprite    — the specific frame currently displayed.
// ─────────────────────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(CharacterVisual), "Initialize")]
static class Patch_CharacterVisual_Initialize
{
    static void Postfix(CharacterVisual __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[CharacterVisual] << Initialize");
        ImposterAtlas atlas = __instance.GetAtlas();
        string atlasName = (object?)atlas != null
            ? (atlas.atlasName ?? atlas.name ?? "?")
            : "null";
        SpriteRenderer sr = __instance.SpriteRenderer;
        Sprite sprite = (object?)sr != null ? sr.sprite : null;
        string spriteName = (object?)sprite != null ? sprite.name : "null";
        Plugin.LogD($"[CharacterVisual] << Initialize | go={__instance.gameObject.name} atlas='{atlasName}' sprite='{spriteName}'");
    }
}
