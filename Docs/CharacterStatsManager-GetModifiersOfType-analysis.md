# `CharacterStatsManager$$GetModifiersOfType<object>` — Decompiled Code Analysis

## Signature

```csharp
public void GetModifiersOfType<T>(List<T> result, bool includeGlobal)
// param_1 = this            (CharacterStatsManager*)
// param_2 = result          (List<T> — the list to fill)
// param_3 = includeGlobal   (bool, packed in longlong — cVar8 = (char)param_3)
// param_4 = hidden Il2CppMethodInfo* for T (shared <object> instantiation)
```

This is the accumulating sibling of `HasModifierOfType<T>`.  
All reference-type variants compile to one shared function; `T` is passed at runtime
in the hidden `param_4` method-info pointer.

---

## Field / offset map

Identical party-traversal layout as `HasModifierOfType<object>` — see that document
for the full offset table.  Key additions here:

| Assembly expression | C# meaning |
|---|---|
| `param_4[7][1]` / `(*...[1])()` | Virtual call through generic method info — `result.Clear()` at entry |
| `(*(code *)param_4[7][2])` | Inner-loop per-handler dispatch — `handler.GetModifiersOfType<T>(result, clearList: false)` |
| `(*(code *)param_4[7][3][1])(param_1, param_2, 0, param_4[7][3])` | Global handler dispatch — `globalGameplayModifiersHandler.GetModifiersOfType<T>(result, clearList: false)` |

The `param_4[7][N]` slots are entries in the IL2CPP shared-generic method-info
table.  They carry the concrete virtual call targets that differ per `T` so that the
single `<object>` function body can dispatch correctly.

---

## Reconstructed C#

```csharp
public void GetModifiersOfType<T>(List<T> result, bool includeGlobal)
{
    if (result == null) goto NullReturn;

    // Clear the output list before filling it.
    result.Clear();   // param_4[7][1] dispatch

    var partyMgr     = Manager<PlayerPartyManager>.Instance;
    var partyMembers = partyMgr.currentPartyCharacters;   // +0xC8

    // Iterate backwards over every character currently in the party.
    for (int i = partyMembers.Count - 1; i >= 0; i--)
    {
        PlayerPartyCharacter character = partyMembers[i];
        CharacterDefinitionId id       = character.characterDefinitionId;  // Character+0x40

        PlayerGameplayModifierHandler handler =
            GetGameplayModifiersHandler(id);

        if (handler == null) goto NullReturn;

        // Append this character's modifiers of type T into result
        // (clearList: false — we're accumulating across all party members).
        handler.GetModifiersOfType<T>(result, clearList: false);
    }

    // Optionally also collect from the global handler
    // (trinkets / permanent class-wide modifiers).
    if (includeGlobal)
        globalGameplayModifiersHandler.GetModifiersOfType<T>(result, clearList: false);

    return;

NullReturn:
    // IL2CPP NRE helper
    throw new NullReferenceException();
}
```

---

## Differences from `HasModifierOfType`

| | `HasModifierOfType<T>` | `GetModifiersOfType<T>` |
|---|---|---|
| Returns | `bool` (first match) | `void` (fills `List<T>`) |
| Short-circuit | Yes — returns immediately on first hit | No — always iterates all party members |
| List clear | N/A | Clears `result` at start |
| Per-handler call | `HasModifier<T>(validateModifier: true)` | `GetModifiersOfType<T>(result, clearList: false)` |
| Global handler | Checked only if party loop found nothing | Always appended regardless of party results |

---

## Relation to ShowEnemyHP / ShowEnemyWeaknesses

`GetModifiersOfType<ShowEnemyHPModifier>` (not directly called from `SetEnemyTarget`)
would return every active instance of the modifier across all party members and the
global handler.  It is likely used elsewhere (e.g. to read modifier parameters if
`ShowEnemyHPModifier` ever gains fields in a future version).

The practical implication for the mod strategy identified in
`CharacterStatsManager-HasModifierOfType-analysis.md` is reinforced here:

Injecting a `ShowEnemyHPModifier` instance into `globalGameplayModifiersHandler` at
mod startup will be found by **both** `HasModifierOfType` (via the `includeGlobal`
branch) and `GetModifiersOfType` (which always appends global results), making the
solution complete and transparent to both call sites with no patches required.
