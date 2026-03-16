# PlayerParty — OnCharacterVariantChanged analysis

## Source

Ghidra decompilation of `PlayerParty$$OnCharacterVariantChanged` (both overloads).  
`PlayerParty.SwapCharacterGameObject` signature from `Dump/dump.cs`.

---

## Reconstructed C# — Single-character overload

```
public void OnCharacterVariantChanged(
    CharacterDefinitionId character,        // struct — NOT patchable
    EPartyCharacterVariant variant,
    bool updateCharacter,
    Action<PlayerParty> onPartyReloadDone)
```

```csharp
void OnCharacterVariantChanged(CharacterDefinitionId character, EPartyCharacterVariant variant,
                                bool updateCharacter, Action<PlayerParty> callback)
{
    if (!updateCharacter) return;         // early-out if model swap not requested

    // param_1+0xa4 == toLoad (int). Buffer slot selection:
    //   toLoad < 1  → partyLoadingData        (param_1[0x15] = offset 0xA8)
    //   toLoad >= 1 → partyLoadingDataBuffer  (param_1[0x16] = offset 0xB0)
    var data = (toLoad < 1) ? partyLoadingData : partyLoadingDataBuffer;

    // null data = assert/bug path (calls FUN_1802845b0 → swi(3))

    data.AddCharacter(character, variant);
    data.AddCallback(callback, enqueue: false, null);

    if (toLoad < 1)
        LoadParty(data);        // ← our Prefix patch fires here
    // else: request queued; LoadParty fires once current load drains toLoad to 0
}
```

**Key detail:** `updateCharacter` is the `bool` 3rd parameter. If the caller passes `false`,
no load happens at all — the variant field is written but the 3D model is NOT reloaded.

---

## Reconstructed C# — List overload

```
public void OnCharacterVariantChanged(
    List<CharacterDefinitionId> characters,  // List<T> (class) — patchable
    EPartyCharacterVariant variant,
    bool updateCharacters,
    Action<PlayerParty> onPartyReloadDone)
```

```csharp
void OnCharacterVariantChanged(List<CharacterDefinitionId> characters, EPartyCharacterVariant variant,
                                bool updateCharacters, Action<PlayerParty> callback)
{
    if (!updateCharacters) return;

    var data = (toLoad < 1) ? partyLoadingData : partyLoadingDataBuffer;
    // null data → assert

    foreach (var character in characters)
        data.AddCharacter(character, variant);

    data.AddCallback(callback, enqueue: false);

    if (toLoad < 1)
        LoadParty(data);        // ← our Prefix patch fires here
}
```

---

## SwapCharacterGameObject

```
public void SwapCharacterGameObject(
    CharacterDefinitionId characterDefinitionId,    // struct — NOT patchable
    PlayerPartyCharacter newCharacterInstance)
```

Takes an **already-instantiated** `PlayerPartyCharacter` and directly replaces the live
slot — **no Addressable load, no `LoadParty` call, our swap patch never fires.**

A cutscene that pre-instantiates the ROBOT model as part of scene setup and then calls
`SwapCharacterGameObject(SeraiId, robotInstance)` at the dramatic moment would produce
exactly the observed symptom: DEFAULT during walk-in, ROBOT on stance change.

---

## Patchability summary

| Method | First param | Patchable? | Notes |
|--------|-------------|------------|-------|
| `OnCharacterVariantChanged(CharacterDefinitionId, ...)` | struct | ❌ | Harmony trampoline silent in IL2CPP |
| `OnCharacterVariantChanged(List<CharacterDefinitionId>, ...)` | `List<T>` (class) | ✅ | Diagnostic added |
| `SwapCharacterGameObject(CharacterDefinitionId, PlayerPartyCharacter)` | struct | ❌ | Bypasses LoadParty entirely |
| `LoadAdditionalCharacter(CharacterDefinitionId, ...)` | struct | ❌ | Harmony trampoline silent in IL2CPP |
| `OnPartyLoaded()` | — | ✅ | Diagnostic added |
| `LoadParty(PartyLoadingData)` | class | ✅ | **Primary swap patch** |

---

## Diagnostic strategy

Patches added behind `Diag.Enabled` const (flip to `true` to activate):

- `Patch_PlayerParty_OnPartyLoaded` — fires when an Addressable load completes
- `Patch_PlayerParty_OnCharacterVariantChanged_List` — fires when the List overload is called; logs `variant` and `updateCharacters`
- `Patch_PlayableCharacterData_SetVariant` — fires when variant is written on `PlayableCharacterData`; logs prev/next

If `SetVariant(ROBOT)` fires but neither `OnCharacterVariantChanged(List)` nor `LoadParty` follow,
the cutscene is calling `SwapCharacterGameObject` directly with a pre-built ROBOT instance.

---

## Fix hypothesis

If `SwapCharacterGameObject` is confirmed as the culprit, the fix must intercept it
**before** the pre-built ROBOT instance is handed to `SwapCharacterGameObject`.
Candidates:

1. Patch `LoadAdditionalCharacter` — but struct first param blocks this.
2. Patch `PlayerPartyCharacter` instantiation or `Awake` to detect ROBOT and redirect
   to the DEFAULT prefab at that point.
3. Walk the `PlayerParty.characters` list in the `SwapCharacterGameObject` call frame
   via a different detour (e.g. postfix on `OnPartyLoaded` to check if a ROBOT instance
   appeared and swap it out immediately).
