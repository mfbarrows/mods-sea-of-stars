# SetCharactersVariantNode — BeforeExecute analysis

## Source

Ghidra decompilation of `SetCharactersVariantNode$$BeforeExecute`.

```
[BehaviorTreeNode("Action/Party/Set Characters Variant", ...)]
public class SetCharactersVariantNode : LeafNode   // TypeDefIndex: 4158
```

---

## Field Layout

From `dump.cs` line 154657:

| Offset | Field | Type |
|--------|-------|------|
| `0x60` | `toSet` | `GraphVariable<EDynamicPlayerPartyCharacter>` |
| `0x68` | `variant` | `GraphVariable<EPartyCharacterVariant>` |
| `0x70` | `reloadParty` | `GraphVariable<bool>` |
| `0x78` | `waitForPartyReloadDone` | `GraphVariable<bool>` |
| `0x80` | `newVariantInstance` | `GraphVariable<GameObject>` |
| `0x88` | `partyReloadDone` | `bool` (private) |

### Ghidra → field mapping (`param_1` = `this`)

| Ghidra expression | Field |
|-------------------|-------|
| `*(longlong *)param_1[6]` | `this.toSet` pointer (at 0x60) |
| `*(longlong *)(param_1[6] + 8)` | `this.variant` pointer (at 0x68) |
| `*(undefined1 (**)[16])param_1[7]` | `this.reloadParty` pointer (at 0x70) |
| `*(longlong *)param_1[8]` | `this.newVariantInstance` pointer (at 0x80) |
| `param_1[8][8] = 0` | `this.partyReloadDone = false` (at 0x88) |

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
    //    NOTE: the loop iterates count times (count-1 downto 0), but
    //    CharacterStatsManager.SetCharactersVariant is called with the *whole*
    //    list on each pass. In practice toSet usually resolves to one character.
    for (int i = count - 1; i >= 0; i--)
    {
        // 3a. Navigate to the CharacterStatsManager's runtime data object
        //     (two levels of lazy-init wrapper → final data blob = puVar2)
        var statsManager  = Manager<CharacterStatsManager>.Instance;
        var statsSubObj   = statsManager.runtimeSubObject;   // offset 0x18 deref
        var statsData     = statsSubObj.dataBlock;           // offset plVar6[0x17]+0x10

        // Refresh the shared list reference (may have moved through lazy-init)
        charIds = SharedLists.Instance.characterIdList;

        // Read variant and reload flag for this iteration
        EPartyCharacterVariant targetVariant = this.variant.Value;
        bool doReload = this.reloadParty.Value;

        // 3b. Create onPartyReloaded callback delegate
        Action<PlayerParty> callback = new Action<PlayerParty>(onPartyReloaded);

        // 3c. Core call: update the variant in stats data; if doReload=true,
        //               this triggers PlayerParty.LoadParty(data) internally
        if (statsData != null)
        {
            CharacterStatsManager.SetCharactersVariant(
                statsData,
                charIds,
                targetVariant,
                doReload,       // updateCharacters / reloadParty
                callback);
        }

        // 3d. If a pre-built variant GameObject is provided, also do a live
        //     scene swap: replace the current PlayerPartyCharacter GO with the
        //     pre-built one.  This path is independent from 3c.
        if (this.newVariantInstance != null)
        {
            GameObject newGO = this.newVariantInstance.Value;

            // Check Unity object is alive (IntPtr null-check pattern)
            if (newGO != null && !ReferenceEquals(newGO, IntPtr.Zero))
            {
                PlayerPartyManager manager = Manager<PlayerPartyManager>.Instance;

                // GetCharacterIdRef: gets a ref/pointer to charIds[0]
                // (the target character's CharacterDefinitionId)
                ref CharacterDefinitionId charId = ref charIds[0];

                // Get the PlayerPartyCharacter component from the pre-built GO
                PlayerPartyCharacter newPPC = newGO.GetComponent<PlayerPartyCharacter>();

                if (manager != null && newPPC != null)
                {
                    // Swap the live scene object for the pre-built ROBOT instance.
                    // This is the call that visually swaps Serai to ROBOT even when
                    // reloadParty=false (bypassing LoadParty entirely).
                    PlayerPartyManager.SwapPartyCharacterGameObject(manager, ref charId, newPPC);
                }
            }
        }
    }
}
```

---

## Key Observations

### 1. Two independent swap mechanisms

| Mechanism | Trigger | Fires LoadParty? |
|-----------|---------|-----------------|
| `CharacterStatsManager.SetCharactersVariant(…, doReload=true, …)` | `reloadParty.Value == true` | **Yes** → our `Patch_PlayerParty_LoadParty_PartyLoadingData` intercepts |
| `PlayerPartyManager.SwapPartyCharacterGameObject(mgr, charId, newPPC)` | `newVariantInstance != null` | **No** → bypasses all LoadParty patches |

### 2. `variant.Value` is re-read each loop iteration

The `variant` GraphVariable is accessed inside the loop body (not hoisted before the loop). A Prefix patch that writes `__instance.variant.Value = EPartyCharacterVariant.DEFAULT` before `BeforeExecute` runs will affect every iteration equally — the rewrite is persistent in the GraphVariable's storage.

### 3. `newVariantInstance` is the dangerous path

If the cutscene BehaviorTree pre-populates `newVariantInstance` with a ROBOT `PlayerPartyCharacter` instance that was placed in the scene ahead of time, `SwapPartyCharacterGameObject` will fire with it regardless of `reloadParty`. This bypasses `LoadParty` entirely and produces the visible ROBOT model switch.

### 4. `waitForPartyReloadDone` (0x78) is NOT used in `BeforeExecute`

It is only checked in `Execute()` to gate NodeStatus.RUNNING vs SUCCESS. It does not influence which swap path runs.

### 5. `onPartyReloaded` callback

`MeshCombineStudio.MeshCombiner.EventMethod$$.ctor(callback, this, Method$SetCharactersVariantNode.onPartyReloaded())` builds a delegate from `this.onPartyReloaded`. This callback is passed to `SetCharactersVariant` and invoked when the party finishes loading; it sets `partyReloadDone = true` to unblock `Execute()`.

---

## `CharacterStatsManager.SetCharactersVariant` Signature

From `dump.cs` line 162248:

```csharp
public void SetCharactersVariant(
    List<CharacterDefinitionId> characters,
    EPartyCharacterVariant variant,
    bool updateCharacters,          // = reloadParty
    Action<PlayerParty> onPartyReloaded)
```

When `updateCharacters = true`, this calls `PlayerParty.LoadParty(data)` with the ROBOT variant baked into the party data, which is then caught by our Prefix patch.

---

## Implications for Mod Fix

### Scenario A — `reloadParty = true`, `newVariantInstance = null`

`SetCharactersVariant(…, ROBOT, true, callback)` → `LoadParty(data)` → **our Prefix patch fires** and swaps the prefab to DEFAULT before any characters are loaded. **Already handled.**

### Scenario B — `reloadParty = true`, `newVariantInstance = non-null`

Same as A (LoadParty fires, our patch intercepts), but THEN `SwapPartyCharacterGameObject` also runs with the pre-built ROBOT GO. The LoadParty-based swap may be immediately overwritten by the GO swap. **Potential second swap problem.**

### Scenario C — `reloadParty = false`, `newVariantInstance = non-null`

`SetCharactersVariant(…, ROBOT, false, callback)` changes stats data only (no LoadParty). Then `SwapPartyCharacterGameObject` runs with the ROBOT GO. **Our LoadParty patch does NOT fire.** Visual swap happens without us.

### Fix strategy (Option B — cleanest)

Prefix `BeforeExecute` and redirect the variant before either mechanism runs:

```csharp
[HarmonyPatch(typeof(SetCharactersVariantNode), "BeforeExecute")]
[HarmonyPrefix]
static void Prefix(SetCharactersVariantNode __instance)
{
    // Only intercept if the target includes Serai
    // EDynamicPlayerPartyCharacter has a flag for Serai — check it
    if (__instance.variant?.Value == EPartyCharacterVariant.ROBOT)
    {
        // Redirect ROBOT → DEFAULT before the node executes
        // This affects BOTH the SetCharactersVariant call AND any GO-swap path
        // because variant.Value is read AFTER our Prefix returns
        __instance.variant.Value = EPartyCharacterVariant.DEFAULT;
    }
}
```

**Caveat:** `GraphVariable<T>.Value` needs a public setter. The dump shows `public T Value { get; }` — setter visibility needs confirming at runtime with Harmony `Traverse` if needed.

### Fix strategy (Option A — fallback)

Postfix `ApplyRenderingSettingsToParty` and walk the live character list. Replace any ROBOT-variant Serai GO with the DEFAULT prefab. Requires knowing how to obtain the DEFAULT prefab reference at that point.

---

## Patchability Summary

| Method | Params | Patchable? |
|--------|--------|-----------|
| `SetCharactersVariantNode.BeforeExecute()` | none | ✅ Yes |
| `CharacterStatsManager.SetCharactersVariant(List<…>, EPartyCharacterVariant, bool, Action<PlayerParty>)` | value types only | ✅ Yes |
| `PlayerPartyManager.SwapPartyCharacterGameObject(CharacterDefinitionId, PlayerPartyCharacter)` | struct first param | ❌ Not directly |
