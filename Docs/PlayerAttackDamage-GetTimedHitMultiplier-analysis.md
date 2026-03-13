# PlayerAttackDamage$$GetTimedHitMultiplier — Decompiled Analysis

Source: Ghidra decompilation of `GameAssembly.dll` VA `0x1804CD840`

## Reconstructed C# (equivalent logic)

```csharp
protected float GetTimedHitMultiplier(float baseMultiplier, HitData hitData)
{
    // ── 1. Null guards ────────────────────────────────────────────────────
    if (hitData == null) goto fail;
    TeamQTEResult qteResult = hitData.qteResult;           // hitData[0x28]
    if (qteResult == null) goto fail;

    // ── 2. Multi-player extra-success bonus (additive) ────────────────────
    // Each QTE success beyond the first adds basicAttackAdditionalTimedHitMultiplierBonus
    // to the multiplier.  Source: GlobalCombatSettings (CombatManager.globalCombatSettings).
    int successCount = TeamQTEResult.GetSuccessCount(qteResult);
    if (successCount > 0)
    {
        // FUN_1800127d0 returns the CombatManager instance using Manager<CombatManager>.get_Instance()
        // CombatManager[0xE0] = globalCombatSettings (GlobalCombatSettings)
        // GlobalCombatSettings[0x1C] = basicAttackAdditionalTimedHitMultiplierBonus
        CombatManager cm = Manager<CombatManager>.get_Instance();
        if (cm != null && cm.globalCombatSettings != null)
        {
            baseMultiplier += (successCount - 1)
                              * cm.globalCombatSettings.basicAttackAdditionalTimedHitMultiplierBonus;
        }
    }

    // ── 3. TimedAttackMultiplierModifier loop ─────────────────────────────
    // Only runs if the attacker is a PlayerCombatActor with a CharacterStatsManager.
    CombatActor attacker = hitData.attacker;               // hitData[0x48]
    if (attacker == null) goto fail;
    PlayerCombatActor playerActor = attacker as PlayerCombatActor;
    if (playerActor == null) goto fail;

    using var modifierList = PooledList<TimedAttackMultiplierModifier>.GetFromPool();
    CharacterStatsManager stats = playerActor.GetGameplayModifierHandler()
                                              .GetComponent<CharacterStatsManager>();
    stats.GetModifiersForCharacter<TimedAttackMultiplierModifier>(modifierList, includeGlobal: true);

    // Walk the list; each modifier's GetModifiedValue(currentMultiplier) replaces
    // the accumulator.  Virtual dispatch via vtable slot 0x4F.
    for (int i = modifierList.Count - 1; i >= 0; i--)
    {
        TimedAttackMultiplierModifier mod = modifierList[i];
        if (mod == null) break;
        baseMultiplier = mod.GetModifiedValue(baseMultiplier);   // vtable[0x4F]
    }

    modifierList.Pool();   // return to pool
    return baseMultiplier;

fail:
    throw NullReferenceException;   // FUN_1802845b0 / swi(3)
}
```

---

## Key observations

### Step 2 — multi-player extra-success additive bonus
- Reads `GlobalCombatSettings.basicAttackAdditionalTimedHitMultiplierBonus` (offset `+0x1C`).
- Applied as `(successCount - 1) * bonus` — so **zero effect in single-player** where there is
  exactly one success.
- Has nothing to do with any relic or installed modifier.

### Step 3 — `TimedAttackMultiplierModifier` loop
- Calls `CharacterStatsManager.GetModifiersForCharacter<TimedAttackMultiplierModifier>`
  on the **attacker** (not the defender, not globally).
- Iterates in **reverse order** (high index → 0) and chains each modifier's
  `GetModifiedValue` into the next — fully multiplicative/arbitrary depending on
  the modifier's concrete implementation.
- `TimedAttackBonusDamageMultiplier.GetModifiedValue` (shared RVA `0x1806DF220`) simply
  returns `field_0x40 * input`, confirmed by the 7-way shared body.
- If the list is **empty** the loop body never executes and `baseMultiplier` is returned
  unchanged.

### Parallel structure with `GetTimedHitBonusDamage`
Both methods follow identical structure:
1. Guard `hitData.qteResult` / `successCount`
2. Apply a global-settings additive per-extra-success scalar
3. Get a `PooledList` of the relevant modifier type from `CharacterStatsManager`
4. Chain `GetModifiedValue` through the list
5. Return pool + return result

---

## Effect on the Adamant Shard question

The relic installs **two** modifiers onto the player's `GameplayModifierHandler`:
- `AutoTimeAttackModifier` — consumed by `CanAutoTimeHit`
- `TimedAttackBonusDamageMultiplier` (multiplier = 0.5) — consumed by step 3 of this
  function (and the parallel `GetTimedHitBonusDamage`)

Our `PerfectTimingAttack` mod Harmony-patches `CanAutoTimeHit` and installs **no
`TimedAttackMultiplierModifier` or `TimedAttackBonusDamageModifier`** into any
player's modifier list.

With an empty list, step 3 loops zero times and `baseMultiplier` passes through
unchanged.

**Confidence: confirmed.** The decompiled native code has no hidden path that applies
a penalty without a live modifier instance. The only two ways the multiplier can
change are the per-extra-success additive (step 2, single-player = 0×) and the
modifier loop (step 3, empty for our mod).

---

## Field offset cross-reference

| Expression in decompiled code | Field | Offset |
|---|---|---|
| `param_3[5]` (`*((longlong****)hitData + 5)`) | `HitData.qteResult` | `0x28` |
| `param_3[9]` (`*((longlong****)hitData + 9)`) | `HitData.attacker` | `0x48` |
| `lVar5[0xE0]` (CombatManager) | `CombatManager.globalCombatSettings` | `0xE0` |
| `globalCombatSettings[0x1C]` | `GlobalCombatSettings.basicAttackAdditionalTimedHitMultiplierBonus` | `0x1C` |
| vtable slot `[0x4F]` | `TimedAttackMultiplierModifier.GetModifiedValue` | virtual slot 20 (IL2CPP vtable index 0x4F) |
