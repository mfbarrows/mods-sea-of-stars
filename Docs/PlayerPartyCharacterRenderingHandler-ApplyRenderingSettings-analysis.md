# PlayerPartyCharacterRenderingHandler — ApplyRenderingSettings analysis

## Source

Ghidra decompilation of `PlayerPartyCharacterRenderingHandler$$ApplyRenderingSettings`.

## Class layout (`dump.cs` line 168261)

```csharp
public class PlayerPartyCharacterRenderingHandler : MonoBehaviour   // TypeDefIndex: 4538
{
    private static int   characterLightsCount;       // 0x0  (static)
    private static int   characterLightsShadowCount; // 0x4  (static)
    private static float sortingIncrement;           // 0x8  (static)
    public  PlayerPartyCharacter character;          // 0x18
}
```

`param_1` in the decompilation is `this` (`PlayerPartyCharacterRenderingHandler*`).

### Key field dereference map

| Ghidra expression | Field |
|---|---|
| `param_1[3]` (= `*(param_1 + 0x18)`) | `this.character` — the `PlayerPartyCharacter` |
| `param_1[3] + 0x40` | `character.characterDefinitionId` (`CharacterDefinitionId` struct at 0x40 inside `Character`) — wait, `Character.characterDefinitionId` is at 0x40, and `PlayerPartyCharacter` inherits from `Character` |
| `*(ulonglong *)(param_1[3] + 0x40)` | `character.characterDefinitionId` value (read as raw uint for the IndexOf call) |
| `*(longlong *)(plVar5 + 3)` at 0x18 | `lVar4` → `SpriteRenderer` component on the character GO |
| `*(float *)(lVar4 + 0x30)` | `SpriteRenderer.sortingOrder` (Unity internal offset 0x30) |
| `PlayerPartyCharacterRenderingHandler_TypeInfo[0x17] + 8` | static field `sortingIncrement` |

---

## Reconstructed C#

```csharp
private void ApplyRenderingSettings()
{
    // ── 1. Apply character light settings using the party leader/follower index ──

    // Get the PlayerPartyManager singleton
    PlayerPartyManager manager = Manager<PlayerPartyManager>.Instance;
    if (manager == null) return;

    // Walk manager → current party leader characters list (at 0xB8 in manager)
    // and find the index of THIS character in that list.
    var leaderList = manager.leaderCharactersList;   // offset 0xB8
    if (leaderList == null) return;

    int leaderIndex = leaderList.IndexOf(this.character);   // List<PlayerPartyCharacter>.IndexOf
    if (leaderIndex != -1)
    {
        ApplyCharacterLightSettings(leaderIndex);
    }

    // ── 2. Apply sprite sorting order based on party member position ────────────

    // Get manager again (second FUN_180012120 call / second null check)
    manager = Manager<PlayerPartyManager>.Instance;
    if (manager == null) return;

    // Walk manager → current party characters list (at 0x98 in manager)
    // character must be non-null and have a non-null SpriteRenderer
    var partyList = manager.currentPartyCharacters;   // offset 0x98
    if (partyList == null) return;
    if (this.character == null) return;

    // Get the character's SpriteRenderer (at 0xF8 in PlayerPartyCharacter → offset [5] from base)
    SpriteRenderer sr = this.character.playerSpriteRenderer;   // 0x100 in PPC
    if (sr == null) return;

    // Find index of THIS character's CharacterDefinitionId in the party-order list
    // The party-order list drives render depth so characters don't z-fight.
    var charIdList = partyList.characterIdOrderList;   // offset [5] from partyList base
    if (charIdList == null) return;

    int partyIndex = charIdList.IndexOf(this.character.characterDefinitionId);
    if (partyIndex == -1) return;   // character not in list → abort

    // Apply sorting: sortingOrder = partyIndex * sortingIncrement  (static field)
    sr.sortingOrder = (int)((float)partyIndex * PlayerPartyCharacterRenderingHandler.sortingIncrement);
}
```

---

## Key Observations

### 1. This function is ENTIRELY about sprite layering — not variant or model

`ApplyRenderingSettings` does exactly two things:
1. **`ApplyCharacterLightSettings`** — sets dynamic character lighting based on leader/follower index.
2. **Sets `SpriteRenderer.sortingOrder`** — controls which character sprite renders on top when characters overlap.

**It reads no variant data, changes no prefabs, and has no bearing on which 3D model is displayed.**

### 2. Why `PlayerPartyManager.ApplyRenderingSettingsToParty` fires at the transition moment

`ApplyRenderingSettingsToParty` iterates `CurrentPartyCharacters` and calls `ApplyRenderingSettings` on each character's `renderingHandler`. It fires on any party membership change (SwapCharacterGameObject, SetupParty, etc.) purely to re-sort sprites and update lighting.

Its appearance in the log right at the ROBOT transition moment is a **consequence** of a party roster change, not the cause of the visual swap. The actual model swap happens in whatever triggers `ApplyRenderingSettingsToParty` upstream.

### 3. Variant data is read by the caller — not here

The `get_CurrentVariant` call that appeared 1 ms before `ApplyRenderingSettingsToParty` in the log is called somewhere in the chain that CALLS `ApplyRenderingSettingsToParty`, not inside this function. Likely `Refresh()` → `ApplyRenderingSettings()` is called from `OnEnable()`, which fires when a character GO is activated after a `SwapCharacterGameObject` swap.

---

## Patchability

| Method | Status |
|---|---|
| `PlayerPartyCharacterRenderingHandler.ApplyRenderingSettings()` | ✅ Private, no params — patchable but useless for mod fix |
| `PlayerPartyCharacterRenderingHandler.Refresh()` | ✅ Public, no params — patchable, fires on GO activation |

---

## Implication for mod investigation

`ApplyRenderingSettingsToParty` is a red herring as a fix target. The swap has already happened by the time it fires. The **real** trigger is what causes a new `PlayerPartyCharacter` GO to become active — either:
- `SwapCharacterGameObject` with a pre-built ROBOT instance
- `SetupParty` activating a ROBOT character that was already in the scene

Watch `EnterCinematicState` / `EnterGameplayState` on `PlayerGameplayToCinematicStateTransitionHandler` — these control the state-machine transitions that accompany GO swaps and are logged with `charId`.
