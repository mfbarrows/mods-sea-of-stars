using HarmonyLib;
namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// PlayerParty patches
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
// NOT patchable (CharacterDefinitionId struct first param):
//   SwapCharacterGameObject, LoadAdditionalCharacter,
//   OnCharacterVariantChanged (single-char overload).
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
    // Prefix fires BEFORE Start() stores scene/position and destroys itself.
    // Logs instance identity so we can compare with LoadPartyCharacter logs.
    static void Prefix(PlayerParty __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerParty] >> Start | " +
            $"instantiatedParty={__instance.instantiatedParty} | " +
            $"instance={__instance.GetHashCode():X}");
    }

    // Postfix fires AFTER Start() — note: the gameObject is already destroyed
    // (or will be destroyed end-of-frame) on the normal path.
    static void Postfix(PlayerParty __instance)
    {
        var variants = __instance.seraiVariants;
        var prefabs  = __instance.seraiVariantsPrefabs;

        if (variants == null || prefabs == null)
        {
            Plugin.LogW($"[PlayerParty] << Start | lists null | instance={__instance.GetHashCode():X}");
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
            Plugin.LogW($"[PlayerParty] << Start | variant indices not found - DEFAULT:{defaultIdx} ROBOT:{robotIdx} (count:{variants.Count}) | instance={__instance.GetHashCode():X}");
            return;
        }

        prefabs[robotIdx] = prefabs[defaultIdx];
        Plugin.LogI($"[PlayerParty] << Start | seraiVariantsPrefabs[{robotIdx}](ROBOT) -> DEFAULT | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// DIAGNOSTIC: expected to be SILENT — CharacterDefinitionId struct as first
/// param prevents the Harmony trampoline from applying in IL2CPP. Kept so we
/// notice if it starts firing (would mean the struct issue is resolved).
/// Signature: private void LoadPartyCharacter(CharacterDefinitionId character,
///   EPartyCharacterVariant characterVariant)
/// </summary>
[HarmonyPatch(typeof(PlayerParty), "LoadPartyCharacter")]
static class Patch_PlayerParty_LoadPartyCharacter
{
    static void Prefix(PlayerParty __instance, EPartyCharacterVariant characterVariant)
    {
        Plugin.LogD($"[PlayerParty] >> LoadPartyCharacter | variant={characterVariant} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// DIAGNOSTIC: fires when a full Addressable party load completes.
/// Useful to confirm LoadParty(data) ran and finished.
/// Signature: private void OnPartyLoaded()
/// </summary>
[HarmonyPatch(typeof(PlayerParty), "OnPartyLoaded")]
static class Patch_PlayerParty_OnPartyLoaded
{
    static void Prefix(PlayerParty __instance)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerParty] >> OnPartyLoaded | instantiatedParty={__instance.instantiatedParty} | instance={__instance.GetHashCode():X}");
    }
}

/// <summary>
/// DIAGNOSTIC: OnCharacterVariantChanged(List overload) — patchable because
/// the first param is List&lt;CharacterDefinitionId&gt; (a class).
/// Both overloads end with LoadParty(data) when toLoad &lt; 1, so our primary
/// swap patch will intercept the load that follows.
/// The bool updateCharacters param gates whether a 3D reload is requested at all.
/// Signature: public void OnCharacterVariantChanged(List&lt;CharacterDefinitionId&gt; characters,
///   EPartyCharacterVariant variant, bool updateCharacters, Action&lt;PlayerParty&gt; onPartyReloadDone)
/// </summary>
[HarmonyPatch]
static class Patch_PlayerParty_OnCharacterVariantChanged_List
{
    static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(PlayerParty))
            .Find(m => m.Name == "OnCharacterVariantChanged"
                    && m.GetParameters()[0].ParameterType.IsGenericType);

    static void Prefix(PlayerParty __instance, EPartyCharacterVariant variant, bool updateCharacters)
    {
        if (!Diag.Enabled) return;
        Plugin.LogD($"[PlayerParty] >> OnCharacterVariantChanged(List) | "
            + $"variant={variant} updateCharacters={updateCharacters} | "
            + $"instance={__instance.GetHashCode():X}");
    }
}

// NOT PATCHABLE (CharacterDefinitionId struct as first param):
// [HarmonyPatch(typeof(PlayerParty), "SwapCharacterGameObject")]
// static class Patch_PlayerParty_SwapCharacterGameObject { }
//   Takes a pre-built PlayerPartyCharacter instance and swaps it directly —
//   no Addressable load, LoadParty is never called, our swap patch never fires.
//   This is the prime suspect for the final-boss cutscene ROBOT swap.

// [HarmonyPatch(typeof(PlayerParty), "LoadAdditionalCharacter")]
// static class Patch_PlayerParty_LoadAdditionalCharacter { }
//   Loads a single character outside the normal LoadParty flow.

// [HarmonyPatch(typeof(PlayerParty), "OnCharacterVariantChanged")]
// static class Patch_PlayerParty_OnCharacterVariantChanged_Single { }
//   Single-character overload — same logic as the List overload but for one
//   character. Calls LoadParty(data) when updateCharacter=true and toLoad < 1.
