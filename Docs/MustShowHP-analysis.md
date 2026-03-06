# `EnemyDescriptionPanel.MustShowHP()` — Ghidra Decompilation Analysis

## Dump signature

```
// RVA: 0xD484D0  Offset: 0xD46CD0  VA: 0x180D484D0
private bool MustShowHP() { }
```

---

## Ghidra decompilation (annotated)

```c
void EnemyDescriptionPanel$$MustShowHP(
        undefined8 *param_1,   // <-- `this`  (EnemyDescriptionPanel*)
        undefined8  param_2,   // <-- IL2CPP hidden MethodInfo* for this callsite
        ulonglong  *param_3,   // \  IL2CPP internal args (exception / stack-unwinding
        longlong ******param_4 // /  infrastructure; ignored by managed code)
) {
    // --- IL2CPP lazy method-table initialization (runs once per process) -----
    if (DAT_183a129c2 == '\0') {
        // Resolve and cache the MethodInfo token for
        // CharacterStatsManager.HasModifierOfType<ShowEnemyHPModifier>()
        FUN_1802a4cc0(
            (longlong *)&Method$CharacterStatsManager.HasModifierOfType<ShowEnemyHPModifier>()
        );
        // Resolve and cache the MethodInfo token for
        // Manager<CharacterStatsManager>.get_Instance()
        param_1 = &Method$Manager<CharacterStatsManager>.get_Instance();
        FUN_1802a4cc0(&Method$Manager<CharacterStatsManager>.get_Instance());
        DAT_183a129c2 = '\x01';   // mark as initialized
    }

    // --- Manager<CharacterStatsManager>.get_Instance() ----------------------
    // Returns the singleton CharacterStatsManager.
    // `param_1` now holds the resolved method pointer (after the init block);
    // FUN_180014930 is the IL2CPP thunk for Manager<T>.get_Instance().
    plVar2 = (longlong *)FUN_180014930(
        param_1, CONCAT71(uVar4, uVar3), param_3, (ulonglong *)param_4
    );

    // --- null check (should never fire in normal gameplay) -------------------
    if (plVar2 != NULL) {
        // Happy path: call instance.HasModifierOfType<ShowEnemyHPModifier>(true)
        CharacterStatsManager$$HasModifierOfType<object>(
            plVar2,                                                    // `this`
            (longlong *)CONCAT71(uVar4, 1),                           // includeGlobal = true  (low byte = 1)
            Method$CharacterStatsManager.HasModifierOfType<ShowEnemyHPModifier>(), // generic MethodInfo*
            param_4
        );
        return;   // return value is whatever HasModifierOfType stored in the return register
    }

    // Null path: throw NullReferenceException, never returns
    FUN_1802845b0(param_1, CONCAT71(uVar4, uVar3), param_3, (char *)param_4);
    pcVar1 = (code *)swi(3);
    (*pcVar1)();
}
```

---

## Reconstructed C#

```csharp
private bool MustShowHP()
{
    // Singleton lookup — same manager that tracks equipment/relic modifiers.
    CharacterStatsManager manager = Manager<CharacterStatsManager>.Instance;

    // includeGlobal: true — searches the global modifier list, not per-character.
    return manager.HasModifierOfType<ShowEnemyHPModifier>(includeGlobal: true);
}
```

---

## Key findings

### 1. `ShowEnemyHPModifier` is a pure marker class

```csharp
// TypeDefIndex: 1744
public class ShowEnemyHPModifier : GameplayModifier
{
    // no fields
    public void .ctor() { }
}
```

It carries no data. Its mere *presence* in the modifier list is the signal.
The Abacus equipment registers one instance of this class into `CharacterStatsManager`
when equipped; `MustShowHP` returns `true` the moment it exists.

### 2. The lookup goes through `CharacterStatsManager`, not `GameplayModifierHandler`

My earlier claim that this calls `GameplayModifierHandler.HasModifier<T>()` was **wrong**.
The actual callee is:

```
CharacterStatsManager.HasModifierOfType<ShowEnemyHPModifier>(bool includeGlobal)
// RVA: 0x11DB230  VA: 0x1811DB230
```

`CharacterStatsManager` is a scene-level singleton that aggregates all equipment,
relic, and status-effect modifiers for the whole party.
`GameplayModifierHandler.HasModifier<T>()` exists separately and is for per-object
modifier lists (e.g. a single actor's buffs).

### 3. `includeGlobal` is hardcoded `true`

`CONCAT71(uVar4, 1)` reconstructs the MethodInfo* with its low byte set to `1`,
which is the `bool includeGlobal` argument. The function always checks globally —
it is not scoped to a single character's stats.

### 4. Null path is dead code in practice

`Manager<CharacterStatsManager>.Instance` is guaranteed non-null for any fight.
The null branch exists only because IL2CPP emits a null-check before every
virtual dispatch; it will never execute during normal gameplay.

---

## Implication for the mod patch

Since `MustShowHP()` is the sole gate between `SetEnemyTarget()` and `ShowHP()`,
a postfix that forces `__result = true` is the correct, minimal intervention:

```csharp
[HarmonyPatch(typeof(EnemyDescriptionPanel), "MustShowHP")]
static class Patch_EnemyDescriptionPanel_MustShowHP
{
    static void Postfix(ref bool __result) => __result = true;
}
```

**Alternative** — inject a `ShowEnemyHPModifier` instance into `CharacterStatsManager`
at scene load. This would make the game believe the Abacus is equipped and would
also trigger any other code that reacts to `ShowEnemyHPModifier` existing
(e.g. `ShowEnemyWeaknessesModifier` has an analogous sibling modifier).
That approach is more invasive and risks side-effects in save data.

**Preferred approach**: postfix patch on `MustShowHP` — one line, zero side-effects.
