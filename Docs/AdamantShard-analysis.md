# Adamant Shard — How Auto-Timing and the Bonus-Damage Penalty Work

## Summary

The Adamant Shard relic auto-times attacks but cuts the timed-hit bonus damage by
50%.  These two effects are **separate modifier instances** bundled together in the
relic's `GameplayModifierList`.  Our `PerfectTimingAttack` mod patches a much
lower-level gate and **does not install either modifier**, so we get the timing
benefit with **no penalty**.

---

## Relevant Classes (dump.cs)

### `AutoTimeAttackModifier` (line 62200)

```
public class AutoTimeAttackModifier : GameplayModifier
    float autoTimeChances;                 // 0x40  probability [0,1]
    List<EPlayerCombatMoveCategory> validMoveTypes; // 0x48
    List<PlayerCombatMoveDefinition>  validMoves;   // 0x50
    bool CanAutoTime(PlayerCombatMoveDefinition moveDefinition) { }
```

A `GameplayModifier` component that lives in the player's
`GameplayModifierHandler.gameplayModifiers` list while the relic is active.

### `AbstractTimedAttackHandler.CanAutoTimeHit` (line 19876)

```
protected bool CanAutoTimeHit(PlayerCombatMoveDefinition moveDefinition) { }
```

Called by `TimedAttackHandler.GetResult` (and `SinglePlayerPlusAttack.GetResult`)
**before** the QTE window is opened.  It queries the attacking character's
`GameplayModifierHandler` for `AutoTimeAttackModifier` instances belonging to this
move definition.  If any is found (and `CanAutoTime()` returns true), the handler
skips user input and directly produces a `QTEResult = Success`.

### `TimedAttackBonusDamageModifier` / `TimedAttackBonusDamageMultiplier` (lines 64893–64921)

```
public abstract class TimedAttackBonusDamageModifier : GameplayModifier
    float GetTimedAttackBonusDamage(float baseBonusDamage, HitData hitData)
    protected abstract float GetModifiedValue(float baseBonusDamage);

public class TimedAttackBonusDamageMultiplier : TimedAttackBonusDamageModifier
    float multiplier;   // 0x40  → set to 0.5 by the Adamant Shard relic
    protected override float GetModifiedValue(float baseBonusDamage)
        => baseBonusDamage * multiplier;
```

A second `GameplayModifier` also installed by the relic.

### `PlayerAttackDamage.GetTimedHitBonusDamage` (line 30641)

```
public float GetTimedHitBonusDamage(float baseBonus, HitData hitData) { }
```

Called during damage calculation whenever a hit is marked as a QTE success.
It calls `CharacterStatsManager.GetModifiersOfType<TimedAttackBonusDamageModifier>`
(line 162098) on the *attacker* to collect all active modifiers, then applies each
one's `GetTimedAttackBonusDamage` in turn.  With no modifier installed this returns
`baseBonus` unchanged.

### `TimedAttackMultiplierModifier` / `MultiplyTimedAttackMultiplierModifier` (lines 64938–64950)

```
public abstract class TimedAttackMultiplierModifier : GameplayModifier
    float GetTimedAttackMultiplier(float baseMultiplier, HitData hitData)
    protected abstract float GetModifiedValue(float baseMultiplier);

public class MultiplyTimedAttackMultiplierModifier : TimedAttackMultiplierModifier
    float multiplier;   // 0x40
    protected override float GetModifiedValue(float baseMultiplier)
        => baseMultiplier * multiplier;
```

A parallel modifier used by `PlayerAttackDamage.GetTimedHitMultiplier` (line 30638).
The Adamant Shard may install one of these as well (or rely solely on the bonus
damage multiplier — either way irrelevant to this analysis).

### `GameplayModifierList` (line 62592)

```
[Serializable]
public class GameplayModifierList
    List<GameObject> modifiersPrefabs;   // 0x10
    void ApplyTo(GameplayModifierHandler modifierHandler) { }
    void RemoveFrom(GameplayModifierHandler modifierHandler) { }
```

The relic ScriptableObject holds a `GameplayModifierList` whose `modifiersPrefabs`
contains **both** `AutoTimeAttackModifier` and `TimedAttackBonusDamageMultiplier`
prefabs.  `ApplyTo` instantiates all of them onto the target character's handler at
once.  That coupling is purely a relic data configuration choice — there is no code
path that links the two effects causally.

---

## End-to-End Flow: Adamant Shard

```
Relic equipped
└─ GameplayModifierList.ApplyTo(playerModifierHandler)
   ├─ AutoTimeAttackModifier  (autoTimeChances=1.0, validMoves=[all])
   └─ TimedAttackBonusDamageMultiplier  (multiplier=0.5)

Combat turn – player attacks
└─ TimedAttackHandler.GetResult(moveDefinition, callback)
   └─ CanAutoTimeHit(moveDefinition)          ← queries modifier handler
      └─ AutoTimeAttackModifier.CanAutoTime() → true
         → skip UI; produce QTEResult=Success immediately

Hit lands (QTEResult=Success)
└─ PlayerAttackDamage.GetTimedHitBonusDamage(baseBonus, hitData)
   └─ CharacterStatsManager.GetModifiersOfType<TimedAttackBonusDamageModifier>
      └─ TimedAttackBonusDamageMultiplier.GetTimedAttackBonusDamage(baseBonus, hitData)
         └─ GetModifiedValue(baseBonus) → baseBonus * 0.5   ← PENALTY APPLIED
```

---

## End-to-End Flow: PerfectTimingAttack Mod

```
Mod loaded
└─ Harmony patches AbstractTimedAttackHandler.CanAutoTimeHit (Postfix)
   – no GameplayModifier is added to anyone's handler

Combat turn – player attacks
└─ TimedAttackHandler.GetResult(moveDefinition, callback)
   └─ CanAutoTimeHit(moveDefinition)          ← original impl returns false
      └─ Harmony Postfix overrides __result = true
         → QTEResult=Success produced

Hit lands (QTEResult=Success)
└─ PlayerAttackDamage.GetTimedHitBonusDamage(baseBonus, hitData)
   └─ CharacterStatsManager.GetModifiersOfType<TimedAttackBonusDamageModifier>
      └─ [empty list – no modifier was installed]
         → returns baseBonus unchanged                       ← NO PENALTY
```

---

## Conclusion

The Adamant Shard penalty (**TimedAttackBonusDamageMultiplier**, multiplier=0.5) is
applied during the *damage calculation* step by walking the attacker's live modifier
list.  Our mod operates entirely at the *QTE grading* step via a Harmony Postfix and
never touches the modifier list.  The two steps are independent:

| | Adamant Shard | PerfectTimingAttack mod |
|---|---|---|
| Mechanism | Installs `AutoTimeAttackModifier` onto handler | Patches `CanAutoTimeHit` return value |
| Penalty modifier present? | Yes — `TimedAttackBonusDamageMultiplier` (×0.5) in handler | **No** — no modifier installed |
| Timed hit bonus received | 50% of base | **100% of base** |

We are **not** incurring the Adamant Shard penalty.  The 50% reduction is strictly a
consequence of having `TimedAttackBonusDamageMultiplier` in the player's
`GameplayModifierHandler`, which only happens when the player actively equips the
relic.
