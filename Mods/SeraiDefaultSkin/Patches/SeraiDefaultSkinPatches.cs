namespace SeraiDefaultSkin.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// SeraiDefaultSkin — strategy overview
//
// Mutate seraiVariantsPrefabs[robotIdx] → default prefab BEFORE
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
