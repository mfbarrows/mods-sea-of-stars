# `CombatActor$$HasModifier<object>` — Decompiled Code Analysis

## Signature

```csharp
public bool HasModifier<T>()
// param_1 = this      (CombatActor*)
// param_2 = hidden Il2CppMethodInfo* for T  (shared <object> instantiation)
// param_3, param_4 = additional IL2CPP hidden parameters
```

This is a **different method from `CharacterStatsManager.HasModifierOfType<T>`**.  
It lives on `CombatActor` and searches that actor's own `CombatTarget` list, not the
party-wide modifier handlers.

---

## Field / offset map

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `*(ulonglong *)(param_1 + 0x70)` | `0x70` | `List<CombatTarget> targets` |
| `*(uint *)(uVar4 + 0x18)` | `List._count` | `targets.Count` |
| `*(undefined **)(lVar2 + 0x10)` | `List._items` | backing `CombatTarget[]` |
| `*(undefined **)(puVar6 + uVar1*8 + 0x20)` | array element | `targets[uVar1]` (`CombatTarget`) |
| `**(longlong **)(param_2 + 0x38)` | `Il2CppMethodInfo+0x38` | vtable slot for `CombatTarget.HasModifier<T>()` |
| `(**(code **)(lVar7 + 8))()` | vtable dispatch | call `target.HasModifier<T>()` |

---

## Reconstructed C#

```csharp
public bool HasModifier<T>()
{
    if (targets == null) goto NullReturn;

    // Iterate backwards over this actor's CombatTargets.
    for (int i = targets.Count - 1; i >= 0; i--)
    {
        CombatTarget target = targets[i];
        if (target == null) goto NullReturn;

        // Delegate to each target's own HasModifier<T>() check.
        if (target.HasModifier<T>())
            return true;
    }

    return false;

NullReturn:
    throw new NullReferenceException();
}
```

---

## Key difference from `CharacterStatsManager.HasModifierOfType<T>`

| | `CombatActor.HasModifier<T>` | `CharacterStatsManager.HasModifierOfType<T>` |
|---|---|---|
| **Scope** | This actor's own `CombatTarget` list | All party members' `PlayerGameplayModifierHandler`s |
| **Delegates to** | `CombatTarget.HasModifier<T>()` | `PlayerGameplayModifierHandler.HasModifier<T>()` |
| **Use case** | "Does this combatant carry modifier T?" | "Does any party member have relic/equipment T?" |
| **`includeGlobal`** | No | Yes — also checks `globalGameplayModifiersHandler` |

The two are part of different modifier systems:

- `CombatActor.targets` holds `CombatTarget` objects that carry **combat-scoped** modifiers
  (applied per-hit, per-turn, via `AddGameplayModifier` during battle).
- `CharacterStatsManager` tracks **persistent equipment/relic** modifiers that survive
  across encounters.

`SetEnemyTarget` calls `CharacterStatsManager.HasModifierOfType<ShowEnemyHPModifier>`,
not `CombatActor.HasModifier<T>` — so this function is not in the HP-display call chain.

---

## Generic dispatch mechanism

`param_2 + 0x38` holds the concrete `CombatTarget.HasModifier<T>()` method pointer
inside the hidden `Il2CppMethodInfo*` struct.  The call:

```c
lVar7 = **(longlong **)(param_2 + 0x38);  // read method entry from type-info
uVar4 = (**(code **)(lVar7 + 8))();       // virtual dispatch to CombatTarget.HasModifier<T>
```

is IL2CPP's shared-generic virtual dispatch pattern — the concrete `T` is carried through
the call chain entirely in the native `Il2CppMethodInfo*` pointer, invisible to Harmony.
This confirms the earlier finding: **there is no managed-layer way to recover T when
patching the shared `<object>` instantiation**.

---

## Early-return false detail

```c
if ((int)uVar1 < 0) {
    return uVar4 & 0xffffffffffffff00;  // == 0 (false)
}
```

When the loop exhausts all targets without finding a match, `uVar4` still holds the
list pointer from the initial read. Masking its low byte to zero produces `0` (false)
without a separate register load. This is a common IL2CPP `return false` pattern in
loop-exhaustion paths.
