# `CharacterStatsManager$$HasModifierOfType<object>` — Decompiled Code Analysis

## Signature

```csharp
public bool HasModifierOfType<T>(bool includeGlobal)
// param_1 = this            (CharacterStatsManager*)
// param_2 = includeGlobal   (bool, packed in longlong — cVar8 = (char)param_2)
// param_3 = Il2CppMethodInfo* for T — the hidden shared-generic type argument
// param_4 = extra IL2CPP hidden parameter
```

IL2CPP compiles all reference-type variants of `HasModifierOfType<T>` into a single
shared function: `HasModifierOfType<object>`. The concrete type `T` is not erased —
it is passed at runtime as a hidden `MethodInfo*` in `param_3`.

---

## Field / offset map

| Assembly expression | Source | C# meaning |
|---|---|---|
| `*(longlong **)(uVar3 + 200)` | `PlayerPartyManager + 0xC8` | `List<PlayerPartyCharacter> currentPartyCharacters` |
| `*(uint *)(plVar7 + 3)` | `List._items.length` at `+0x18` | `Count` of the backing array (capacity) |
| `*(uint *)(lVar4 + 0x18)` | `List._count` | actual `Count` of the List |
| `*(longlong **)(lVar4 + 0x10)` | `List._items` | backing `PlayerPartyCharacter[]` |
| `plVar7[(longlong)(int)uVar1 + 4]` | array element at `+0x20 + uVar1*8` | `PlayerPartyCharacter` at index `uVar1` |
| `param_2[8]` | `Character + 0x40` | `CharacterDefinitionId characterDefinitionId` |
| `(**(code **)(*(longlong *)(param_3[7] + 8) + 8))(param_1)` | virtual dispatch via `param_3` type info | `this.globalGameplayModifiersHandler.HasModifier<T>(true)` |

---

## Reconstructed C#

```csharp
public bool HasModifierOfType<T>(bool includeGlobal)
{
    var partyMgr     = Manager<PlayerPartyManager>.Instance;
    var partyMembers = partyMgr.currentPartyCharacters;   // List<PlayerPartyCharacter>

    // Iterate backwards over every character currently in the party.
    for (int i = partyMembers.Count - 1; i >= 0; i--)
    {
        PlayerPartyCharacter character = partyMembers[i];
        CharacterDefinitionId id       = character.characterDefinitionId;

        PlayerGameplayModifierHandler handler =
            GetGameplayModifiersHandler(id);

        if (handler.HasModifier<T>(validateModifier: true))
            return true;
    }

    // If includeGlobal is true, also check the global modifier handler
    // (trinkets / permanent modifiers active for the whole party).
    if (includeGlobal && globalGameplayModifiersHandler.HasModifier<T>(validateModifier: true))
        return true;

    return false;
}
```

---

## Logic flow detail

### Class initialisation guard
```c
if (DAT_183a14386 == '\0') {
    // Lazy-init: ensure List<PlayerPartyCharacter>.get_Count / get_Item
    // and Manager<PlayerPartyManager>.get_Instance are resolved.
    DAT_183a14386 = '\x01';
}
```
Standard IL2CPP one-time static initialiser — runs at most once per process.

### Party member loop
The loop iterates **backwards** (`uVar1 = Count - 1` down to `0`).  
Early-exit on the first `handler.HasModifier<T>()` that returns `true`.

The access chain per iteration:
```
currentPartyCharacters[i]
    → character.characterDefinitionId          (Character + 0x40)
    → GetGameplayModifiersHandler(id)           → PlayerGameplayModifierHandler
    → handler.HasModifier<T>(validateModifier: true)
```

### `includeGlobal` branch
After the loop exits without finding a match, if `includeGlobal == true`:
```c
(**(code **)(*(longlong *)(param_3[7] + 8) + 8))(param_1)
```
This is a virtual call via the hidden `Il2CppMethodInfo*` (`param_3`) type-info:
it dispatches `globalGameplayModifiersHandler.HasModifier<T>(true)`, checking the
`GlobalGameplayModifierHandler` (field `CharacterStatsManager.globalGameplayModifiersHandler`
at offset `0x78`) which holds class-wide permanent modifiers like trinket effects.

---

## Implications for ShowEnemyHP / ShowEnemyWeaknesses

`SetEnemyTarget` calls:
```csharp
Manager<CharacterStatsManager>.Instance
    .HasModifierOfType<ShowEnemyHPModifier>(includeGlobal: true)
```

For this to return `true`, one of the following must hold:

1. **Some party member's `PlayerGameplayModifierHandler` contains a `ShowEnemyHPModifier`
   instance** — this is the normal path when the Abacus relic is equipped.
2. **`globalGameplayModifiersHandler` contains a `ShowEnemyHPModifier` instance** —
   this would be the path for a permanent/trinket-style unlock.

### Why patching `HasModifierOfType` is dangerous
IL2CPP compiles all `<T>` variants to one shared function (`HasModifierOfType<object>`).  
A Harmony postfix cannot distinguish `T = ShowEnemyHPModifier` from
`T = AutoTimeAttackModifier`, `T = AutoResurrectionModifier`, etc. without
reading `param_3` (the hidden `Il2CppMethodInfo*`), which is not exposed by Harmony's
standard `__instance` / `__result` injection.

### The cleanest mod path
Inject a `ShowEnemyHPModifier` instance directly into
`CharacterStatsManager.globalGameplayModifiersHandler.gameplayModifiers`
(the `List<GameplayModifier>` at `GameplayModifierHandler + 0x18`) at startup.
`HasModifierOfType` will then find it naturally on the `includeGlobal` branch
without requiring any patched methods or unsafe pointer tricks.

The obstacle is that `AddGameplayModifier` requires a `GameObject` prefab.
However, since `ShowEnemyHPModifier` has no fields — it's a pure marker class
with only a default `.ctor()` — a new `GameObject` with the component added
at runtime is sufficient:

```csharp
// In Plugin.Awake / OnSceneLoaded, after CharacterStatsManager is available:
var csm = Manager<CharacterStatsManager>.Instance;
var go  = new GameObject("ShowEnemyHPModifier");
go.AddComponent<ShowEnemyHPModifier>();
UnityEngine.Object.DontDestroyOnLoad(go);
csm.globalGameplayModifiersHandler.AddGameplayModifier(go);
```

This avoids patches entirely and works with the game's own modifier lookup system.
