# PlayerParty — SwapCharacterGameObject analysis

## Source

Ghidra decompilation of `PlayerParty$$SwapCharacterGameObject`.

```
public void SwapCharacterGameObject(
    CharacterDefinitionId characterDefinitionId,    // struct — NOT patchable
    PlayerPartyCharacter newCharacterInstance)
```

---

## Reconstructed C#

```csharp
void SwapCharacterGameObject(CharacterDefinitionId characterId, PlayerPartyCharacter newChar)
{
    // 1. Find existing character in live party list
    PlayerPartyCharacter oldChar = GetCharacter(characterId);   // param_1 + 0x98 = this.characters
    int oldIndex = this.characters.IndexOf(oldChar);

    // 2. Reparent new char under PlayerParty transform (always, even if oldChar null)
    newChar.gameObject.transform.parent = this.transform;

    bool newIsPlayerController = newChar is PlayerController;
    PlayerController newAsPC = newIsPlayerController ? (PlayerController)newChar : null;

    if (oldChar == null)
    {
        // ── NULL path: no existing character, just place newChar at local origin ──
        newChar.gameObject.transform.localPosition = Vector3.zero;
        this.characters.Add(newChar);
        return;
    }

    // ── SWAP path: oldChar exists ──
    bool oldIsPlayerController = oldChar is PlayerController;
    PlayerController oldAsPC = oldIsPlayerController ? (PlayerController)oldChar : null;

    // 3. Transfer world position
    Vector3 oldWorldPos = oldChar.gameObject.transform.position;
    newChar.gameObject.transform.position = oldWorldPos;

    // 4. Transfer look direction
    LookDirectionController.SetAngle(newChar.lookDir, oldChar.lookDir.angle, snap: true);

    // 5. Transfer active state
    bool wasActive = oldChar.gameObject.activeSelf;
    newChar.gameObject.SetActive(wasActive);

    // 6. Transfer state machine state (idle/walking/etc)
    StateMachineState currentState = oldChar.stateMachine.currentState;        // oldChar[0x12][0xb]
    StateMachineState idleEquiv   = GetCorrespondingState(currentState, newChar);
    newChar.stateMachine.SetState(idleEquiv);

    // 7. PlayerController-specific: transfer input state if both are PlayerControllers
    if (newAsPC != null && oldAsPC != null && !oldAsPC.IsNull && !newAsPC.IsNull)
    {
        // Transfer PlayerController state machine state
        StateMachineState pcState = GetCorrespondingState(oldAsPC.stateMachine.currentState, newAsPC);
        newAsPC.stateMachine.SetState(pcState);
    }

    // 8. Splice new char into same slot, remove old, destroy old GameObject
    this.characters.Insert(oldIndex, newChar);
    this.characters.Remove(oldChar);
    Object.Destroy(oldChar.gameObject);   // ← old character DESTROYED

    // 9. If newChar is PlayerController, update water probe
    if (newAsPC != null && !newAsPC.IsNull)
        WaterProbe.UpdateSweepPosition(newAsPC.waterProbe);

    // 10. Update rendering settings for entire party
    PlayerPartyManager.Instance.ApplyRenderingSettingsToParty();
}
```

---

## Key observations

### It destroys the old GameObject
`Object.Destroy(oldChar.gameObject)` — the old character is completely removed from the scene.
This is a hard, irreversible live swap at runtime.

### It does NOT trigger LoadParty
No Addressable load, no `GetCharacterVariantReference`, no `LoadPartyCharacter` — the `newChar`
instance is already fully constructed before this method is called.
Our `seraiVariantsPrefabs` swap never fires for this path.

### Where does the newChar instance come from?
Two candidates:
1. **`LoadAdditionalCharacter(SeraiId, ROBOT, callback)`** — triggers an Addressable load using
   `GetCharacterVariantReference(SeraiId, ROBOT)`. Since we've already swapped
   `seraiVariantsPrefabs[ROBOT] = seraiVariantsPrefabs[DEFAULT]` at `LoadParty` time, this
   SHOULD return the DEFAULT prefab → instantiates a DEFAULT `PlayerPartyCharacter`.
   If this path is used, our fix already works.
2. **Pre-placed scene object** — a ROBOT `PlayerPartyCharacter` GameObject already exists in the
   Unity scene hierarchy (placed at design time). It bypasses all Addressable loading and our swap.
   This is the path that would produce the observed symptom.

---

## Why the logs don't show the cutscene swap

The log excerpt ends at `11:47:57` (initial party load). The ROBOT swap presumably happens later.
`SetVariant` is NOT logged during or after the cutscene in the provided excerpt — meaning either:
- `SetVariant` was not called at all (swap done purely via `SwapCharacterGameObject` on
  a pre-placed scene object), or
- The diagnostic log ended before the cutscene.

---

## Patchability

| Method | Verdict |
|--------|---------|
| `SwapCharacterGameObject(CharacterDefinitionId, PlayerPartyCharacter)` | ❌ struct first param |
| `LoadAdditionalCharacter(CharacterDefinitionId, ...)` | ❌ struct first param |
| `ApplyRenderingSettingsToParty()` | ✅ no params — fires at END of every `SwapCharacterGameObject` call |

---

## Fix strategy

### Option A — Hook `ApplyRenderingSettingsToParty` Postfix
`PlayerPartyManager.ApplyRenderingSettingsToParty()` is called at the very end of
`SwapCharacterGameObject`. A Postfix patch can walk `PlayerPartyManager.Instance.CurrentPartyCharacters`
(or `PlayerParty.characters`) and check whether any live `PlayerPartyCharacter` is the ROBOT model.

Challenge: `PlayerPartyCharacter` doesn't have a direct C#-visible variant field. You'd need to
compare the live GameObject name or look at an animator/renderer component name to detect ROBOT.

### Option B — Hook `PlayerPartyCharacter` lifecycle
Patch `PlayerPartyCharacter.Awake` or `Start` — these fire with no struct params when any
party character is instantiated. If the ROBOT prefab is about to come alive, we could detect
it here and prevent or redirect the instantiation.

Challenge: detecting "is this the ROBOT Serai" from inside Awake is non-trivial without a
clear discriminator field visible from C#.

### Option C — Hook PlayerParty.OnPartyCharacterLoaded (verify)
`private void OnPartyCharacterLoaded(AsyncOperationHandle<GameObject> handle)` — fires when
an Addressable party character load completes. `AsyncOperationHandle<T>` is a struct, so this
probably can't be patched directly either.

### Recommended next step
Add a Postfix on `ApplyRenderingSettingsToParty` with `Diag.Enabled` to dump the name of every
`PlayerPartyCharacter` in the current party. This will confirm whether a ROBOT instance actually
entered the list, and at what point during the cutscene.
