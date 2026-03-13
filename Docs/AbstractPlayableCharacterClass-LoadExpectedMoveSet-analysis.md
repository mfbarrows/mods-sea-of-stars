# `AbstractPlayableCharacterClass.LoadExpectedMoveSet` — Analysis

**VA:** `0x180A85D60`

---

## Field offset table

| Ghidra expression | Byte offset | Field (from dump.cs) |
|---|---|---|
| `param_1 + 0x1e` (longlong\*) | `0xF0` | `this.currentMoveSet` (`PlayerCombatMoveSetNew`) |
| `param_1[0x1f]` | `0xF8` | `this.currentFighterDefinition` (`PlayableFighterDefinition`) |
| `param_1[0x15]` | `0xA8` | `this.characterData` (`PlayableCharacterData`) |
| `characterData[7]` | `0x38` on characterData | inner string of `characterData.characterId` (`CharacterDefinitionId`) |
| `currentMoveSet[5]` (bool) | `0x28` on `PlayerCombatMoveSetNew` | `movesLoaded` |
| `currentMoveSet[4]` (List) | `0x20` on `PlayerCombatMoveSetNew` | `combatMoveDefinitions` |

---

## Reconstructed C# logic

```csharp
void LoadExpectedMoveSet()
{
    // Step 1 — determine expected move set for the current variant/class.
    // GetMoveSet() does the variant→dict lookup on moveSetByVariant internally.
    PlayerCombatMoveSetNew expected = GetMoveSet();
    PlayerCombatMoveSetNew current  = this.currentMoveSet;   // 0xF0

    // Step 2 — early-out if expected == current (nothing to change).
    // Three sub-cases handled by the nested null/IntPtr checks:
    //   a) both null → same
    //   b) one null, one non-null → different
    //   c) both non-null → compare by reference
    // (The IntPtr_TypeInfo branches are IL2CPP's boxed-null representation.)
    bool same = (expected == null && current == null)
             || (expected != null && current != null && ReferenceEquals(expected, current));

    if (same)
    {
        // "already loaded?" guard — only skip if movesLoaded == true.
        if (this.currentMoveSet == null) throw; // LAB_180a861ad (NullRef)
        if (this.currentMoveSet.movesLoaded) return;
        // else fall through to prefab loading below (moveSet exists but not yet loaded)
    }
    else
    {
        // Step 3 — swap: unload old, assign new.
        UnloadCurrentMoveSet();
        this.currentMoveSet = expected;    // + IL2CPP write barrier
    }

    // Step 4 — resolve fighter definition from CombatManager.
    // FUN_1800127d0 validates / retrieves the CombatManager singleton instance.
    var combatManager = CombatManager.GetInstance();
    if (this.characterData == null || combatManager == null) throw;

    CharacterDefinitionId charId = this.characterData.characterId;
    this.currentFighterDefinition =                            // 0xF8
        combatManager.GetFighterDefinition(charId);            // + write barrier

    // Step 5 — kick off async prefab loads for each move definition.
    var moveSet    = this.currentMoveSet;
    var fighterDef = this.currentFighterDefinition;
    if (fighterDef == null || moveSet == null) throw;

    if (moveSet.movesLoaded) return;         // re-entry guard
    moveSet.movesLoaded = true;              // mark before iterating

    foreach (PlayerCombatMoveDefinition moveDef in moveSet.combatMoveDefinitions)
    {
        if (!moveDef.IsLoaded)
            moveDef.LoadCombatMovePrefab();  // async Addressable load
    }
    fighterDef.AddLoadedMoves(moveSet);
}
```

---

## Key finding: `GetMoveSet()` bypasses `characterData.currentVariant` for ROBOT

`LoadExpectedMoveSet` calls `GetMoveSet()` on the **class instance** (`AbstractPlayableCharacterClass`), which owns:

```
public bool overrideVariant;              // 0x90
public EPartyCharacterVariant classVariant; // 0x94
public MoveSetByVariant moveSetByVariant;   // 0x78
```

The most natural implementation of `GetMoveSet()` is:

```csharp
PlayerCombatMoveSetNew GetMoveSet() =>
    overrideVariant
        ? moveSetByVariant[classVariant]                       // class-level override
        : moveSetByVariant[characterData.CurrentVariant];      // player save data
```

For the **ROBOT class**: `overrideVariant = true`, `classVariant = ROBOT`.  
→ `GetMoveSet()` reads `classVariant` directly and **never calls `characterData.get_CurrentVariant`**.

This needs to be confirmed by decompiling `GetMoveSet` (VA `0x180A861D0`), but is strongly implied by the ScriptableObject field layout.

---

## Implication for the `SeraiDefaultSkin` mod

Patching `PlayableCharacterData.get_CurrentVariant` to return `DEFAULT` when the stored value is `ROBOT` **does not affect move set selection** when the ROBOT class is equipped, because `GetMoveSet()` uses `classVariant` directly via `overrideVariant`. The patch only redirects:

- `PlayerParty.GetCharacterVariantReference()` — visual prefab lookup
- `PlayerParty.LoadPartyCharacter()` — party spawn
- Animation clip override selection (`LoadAnimClipOverride`)
- Any other consumer that reads `currentVariant` from save data rather than from the class instance

The move set, fighter definition, and combat behaviour are untouched. This is the desired outcome.

---

## Still to confirm

- Decompile `AbstractPlayableCharacterClass.GetMoveSet` (VA `0x180A861D0`) to verify the `overrideVariant` branch
- Check whether DEFAULT and ROBOT have different `moveSetByVariant` entries in the Unity assets (in-game testing so far shows no difference in available skills)
