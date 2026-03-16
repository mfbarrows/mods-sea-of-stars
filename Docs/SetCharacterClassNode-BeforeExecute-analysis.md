# SetCharacterClassNode — BeforeExecute analysis

## Source

Ghidra decompilation of `SetCharacterClassNode$$BeforeExecute`.

```
[BehaviorTreeNode("Action/Party/Set Character Class", ...)]
public class SetCharacterClassNode : LeafNode   // TypeDefIndex: 4155
```

---

## Field Layout

From `dump.cs` line 154548:

| Offset | Field | Type |
|--------|-------|------|
| `0x60` | `toSet` | `GraphVariable<EDynamicPlayerPartyCharacter>` |
| `0x68` | `characterClass` | `GraphVariable<AbstractPlayableCharacterClass>` |
| `0x70` | `reloadParty` | `GraphVariable<bool>` |
| `0x78` | `waitForPartyReloadDone` | `GraphVariable<bool>` |
| `0x80` | `newVariantInstance` | `GraphVariable<GameObject>` |
| `0x88` | `partyReloadDone` | `bool` (private) |

### Ghidra → field mapping (`param_1` = `this`)

| Ghidra expression | Field |
|-------------------|-------|
| `*(longlong *)param_1[6]` | `this.toSet` pointer (at 0x60) |
| `*(undefined1 (**)[16])(param_1[6] + 8)` | `this.characterClass` pointer (at 0x68) |
| `*(undefined1 (**)[16])param_1[7]` | `this.reloadParty` pointer (at 0x70) |
| `*(longlong *)param_1[8]` | `this.newVariantInstance` pointer (at 0x80) |
| `param_1[8][8] = 0` | `this.partyReloadDone = false` (at 0x88) |

---

## `AbstractPlayableCharacterClass` key fields (from `dump.cs` line 15059)

These are the `lVar4 + offset` accesses once `characterClass.Value` has been obtained:

| Offset | Field | Type | Role in `BeforeExecute` |
|--------|-------|------|------------------------|
| `0x60` | `classType` | `EPlayableCharacterClassType` | Passed to `PlayableCharacterData.SetClass` |
| `0x90` | `overrideVariant` | `bool` | Guards the `SetCharactersVariant` call |
| `0x94` | `classVariant` | `EPartyCharacterVariant` | Passed to `SetCharactersVariant` as the variant to set |

Key insight: **a class definition can carry its own variant override.** When `overrideVariant = true` (e.g., the ROBOT class definition has `classVariant = ROBOT`), setting the class *also* triggers a variant change through `SetCharactersVariant`.

---

## Reconstructed C#

```csharp
public override void BeforeExecute()
{
    // 1. Reset the "reload done" flag used by Execute() to detect completion
    this.partyReloadDone = false;

    // 2. Resolve target character set to a concrete list of CharacterDefinitionIds
    EDynamicPlayerPartyCharacter targetCharset = this.toSet.Value;
    List<CharacterDefinitionId> charIds = SharedLists.Instance.GetList<CharacterDefinitionId>();
    EPlayerPartyCharacterExtension.GetCharacterIds(targetCharset, charIds);

    int count = charIds.Count;
    if (count == 0) return;

    // 3. Process each character in the resolved list
    for (int i = count - 1; i >= 0; i--)
    {
        // ── 3a. Optional: swap the scene GO using newVariantInstance ──────────────
        if (this.newVariantInstance != null)
        {
            GameObject newGO = this.newVariantInstance.Value;

            if (newGO != null && !ReferenceEquals(newGO, IntPtr.Zero))
            {
                PlayerPartyManager manager = Manager<PlayerPartyManager>.Instance;

                ref CharacterDefinitionId charId = ref charIds[0];
                PlayerPartyCharacter newPPC = newGO.GetComponent<PlayerPartyCharacter>();

                if (manager != null && newPPC != null)
                {
                    // Pre-built variant GO swap — same path as SetCharactersVariantNode
                    PlayerPartyManager.SwapPartyCharacterGameObject(manager, ref charId, newPPC);
                }
            }
        }

        // ── 3b. Read the class definition ────────────────────────────────────────
        AbstractPlayableCharacterClass classDefinition = this.characterClass.Value;
        if (classDefinition == null) return;

        // ── 3c. Conditional: if the class definition carries a variant override,
        //        also update the character's variant (e.g. ROBOT class → ROBOT variant)
        if (classDefinition.overrideVariant)   // bool field at 0x90
        {
            // Navigate to CharacterStatsManager runtime data (two lazy-init levels)
            var statsManager = Manager<CharacterStatsManager>.Instance;
            var statsData    = statsManager.runtimeSubObject.dataBlock;  // plVar6[0x17]+0x10

            // Refresh shared list
            charIds = SharedLists.Instance.characterIdList;

            // FUN_180012650(this.reloadParty) — reads reloadParty.Value as bool
            bool doReload = this.reloadParty.Value;

            // Create onPartyReloaded callback
            Action<PlayerParty> callback = new Action<PlayerParty>(onPartyReloaded);

            if (statsData != null)
            {
                // Sets the variant carried by the class definition.
                // If doReload=true, triggers PlayerParty.LoadParty internally.
                CharacterStatsManager.SetCharactersVariant(
                    statsData,
                    charIds,
                    classDefinition.classVariant,  // EPartyCharacterVariant at 0x94
                    doReload,
                    callback);
            }
        }

        // ── 3d. Always: update the class data on PlayableCharacterData ────────────
        ref CharacterDefinitionId currentId = ref charIds[i];

        PlayableCharacterData charData =
            CharacterStatsManager.Instance.GetCharacterData(currentId);
        if (charData == null) return;

        // Reads classType at offset 0x60 on the class definition object
        charData.SetClass(classDefinition.classType, load: true);

        // ── 3e. Get the live PlayerPartyCharacter and register new class states ──
        PlayerPartyCharacter ppc =
            Manager<PlayerPartyManager>.Instance.GetPartyCharacter(currentId);

        if (ppc != null)
        {
            // Transfer the class's additional states into the character's state machine
            // ppc.stateMachine.RegisterStates(classDefinition.additionalStates)
            ppc.stateMachine.RegisterStates(classDefinition.additionalStates);
        }
    }

    // 4. After the loop: fire a final delegate if one was registered
    //    (from pauVar11.field0x48 — likely an "onComplete" event)
}
```

> **Note:** Steps 3d and 3e always execute regardless of `overrideVariant`. Step 3c (variant change via `SetCharactersVariant`) is guarded by `classDefinition.overrideVariant`.

---

## Comparison with `SetCharactersVariantNode`

| Aspect | `SetCharactersVariantNode` | `SetCharacterClassNode` |
|--------|---------------------------|------------------------|
| Primary action | Set `EPartyCharacterVariant` | Set `EPlayableCharacterClassType` |
| Variant change | Always (`this.variant.Value`) | Only if `classDefinition.overrideVariant` |
| `CharacterStatsManager.SetCharactersVariant` | Always called | Only called if `overrideVariant=true` |
| `PlayableCharacterData.SetClass` | Never called | Always called |
| `StateMachine.RegisterStates` | Never called | Always called |
| `newVariantInstance` GO swap | If non-null | If non-null (same logic) |
| `SinglePlayerPlusManager` | Not referenced | Referenced (init block only; likely coop check) |

---

## Key Observations

### 1. ROBOT class definition controls whether a variant swap also fires

The ROBOT `AbstractPlayableCharacterClass` ScriptableObject likely has:
- `classType = ROBOT`
- `overrideVariant = true`
- `classVariant = ROBOT`

This means `SetCharacterClassNode` with the ROBOT class triggers **both**:
1. `PlayableCharacterData.SetClass(ROBOT, load: true)` — changes save data class
2. `CharacterStatsManager.SetCharactersVariant(…, ROBOT, reloadParty, callback)` — changes variant (and possibly calls `LoadParty`)

### 2. `reloadParty` works identically to `SetCharactersVariantNode`

Both nodes pass `reloadParty.Value` as the `updateCharacters` parameter to `SetCharactersVariant`. When `true`, LoadParty is called. Our existing Prefix patch on `PlayerParty.LoadParty` intercepts that.

### 3. `SetClass(classType, load: true)` is NOT a visual operation

`PlayableCharacterData.SetClass` only updates the in-memory class data and fires `onClassChanged`. Our mod **wants** this to still run as-is so the save file class stays ROBOT. No need to intercept it.

### 4. `StateMachine.RegisterStates` is safe to leave alone

This updates the combat state machine (moves, actions) to match the new class. Our mod wants Serai to retain ROBOT moves, so this should run as-is.

### 5. Loop vs. single-character

In practice, `toSet` for a specific named character (like Serai) resolves to exactly one `CharacterDefinitionId`. The loop is a generalization for `EDynamicPlayerPartyCharacter` values that map to multiple characters (e.g. "whole party").

---

## Implications for Mod Fix

### Which scenario fires for the final-boss cutscene?

| `overrideVariant` | `reloadParty` | `newVariantInstance` | Result |
|-------------------|--------------|----------------------|--------|
| true | true | null | `SetCharactersVariant(ROBOT, reload=true)` → LoadParty → **our patch fires** ✅ |
| true | true | non-null | LoadParty fires (our patch works) + GO swap overwrites ⚠️ |
| true | false | non-null | Stats data updated silently; GO swap fires ❌ |
| false | — | non-null | Only GO swap; no variant in stats ❌ |

### Fix — Prefix `BeforeExecute` to neutralise the two visual mechanisms

```csharp
[HarmonyPatch(typeof(SetCharacterClassNode), "BeforeExecute")]
[HarmonyPrefix]
static void Prefix(SetCharacterClassNode __instance)
{
    // If the class definition would also force a ROBOT variant override,
    // clear the override flag so SetCharactersVariant is never called with ROBOT.
    // (classDefinition.overrideVariant = false means step 3c is skipped entirely)
    //
    // We still let SetClass(ROBOT) run — that's intentional (save file stays ROBOT).
    // We still let RegisterStates(ROBOT class states) run — ROBOT moves intentional.
    //
    // Accessing the backing field requires Traverse since overrideVariant may be
    // serialized as a Unity SerializeField (no public setter).
}
```

**Important:** the GO swap via `newVariantInstance` is a harder problem regardless of which node runs. If the cutscene pre-places a ROBOT GO in the scene and stores it in `newVariantInstance`, this node (and `SetCharactersVariantNode`) will always call `SwapPartyCharacterGameObject` with it. The Postfix `ApplyRenderingSettingsToParty` approach may be necessary as a second layer for that case.

---

## Patchability Summary

| Method | Status |
|--------|--------|
| `SetCharacterClassNode.BeforeExecute()` | ✅ No params, Harmony-patchable |
| `PlayableCharacterData.SetClass(EPlayableCharacterClassType, bool)` | ✅ Value params only |
| `CharacterStatsManager.SetCharactersVariant(…)` | ✅ Value params only |
| `PlayerPartyManager.SwapPartyCharacterGameObject(CharacterDefinitionId, PlayerPartyCharacter)` | ❌ Struct first param |
| `StateMachine.RegisterStates(List<StateMachineState>)` | ✅ Ref param only |
