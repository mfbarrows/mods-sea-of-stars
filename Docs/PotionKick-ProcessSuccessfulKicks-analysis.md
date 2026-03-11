# Analysis: `PotionKick$$ProcessSuccessfulKicks`

## Background

`ProcessSuccessfulKicks` is called every frame from `PotionKick.UpdateKicks()` **after**
`UpdateKickForPlayer` has run for all SPP players.  It iterates `kickablePotions` and, for
each potion whose `TeamQTEResult` has at least one success registered, fires `KickPotion` to
physically launch the projectile toward the enemy.

This is the **sole caller of `KickPotion`** in the entire class.

---

## Original Ghidra C

```c
void PotionKick$$ProcessSuccessfulKicks
               (undefined8 *param_1,longlong *param_2,ulonglong param_3,longlong ******param_4)

{
  uint uVar1;
  longlong lVar2;
  undefined8 *puVar3;
  ulonglong uVar4;
  code *pcVar5;
  uint uVar6;
  char *pcVar7;
  undefined8 *puVar8;
  ulonglong uVar9;
  longlong *plVar10;
  bool bVar11;
  undefined8 *local_res8;
  
  puVar8 = param_1;
  if (DAT_183a0f754 == '\0') {
    FUN_1802a4cc0((longlong *)&Method$System.Collections.Generic.List<Projectile>.get_Count());
    puVar8 = (undefined8 *)&Method$System.Collections.Generic.List<Projectile>.get_Item();
    FUN_1802a4cc0((longlong *)&Method$System.Collections.Generic.List<Projectile>.get_Item());
    DAT_183a0f754 = '\x01';
  }
  if (param_1[0x33] != 0) {
    uVar1 = *(uint *)(param_1[0x33] + 0x18);
    while( true ) {
      uVar1 = uVar1 - 1;
      if ((int)uVar1 < 0) {
        return;
      }
      lVar2 = param_1[0x33];
      if (lVar2 == 0) break;
      if (*(uint *)(lVar2 + 0x18) <= uVar1) {
        System.ThrowHelper$$ThrowArgumentOutOfRange_IndexException();
        pcVar5 = (code *)swi(3);
        (*pcVar5)();
        return;
      }
      puVar8 = *(undefined8 **)(lVar2 + 0x10);
      if (puVar8 == (undefined8 *)0x0) break;
      if (*(uint *)(puVar8 + 3) <= uVar1) {
        FUN_1802845c0(puVar8,param_2,param_3,(char *)param_4);
        pcVar5 = (code *)swi(3);
        (*pcVar5)();
        return;
      }
      plVar10 = (longlong *)puVar8[(longlong)(int)uVar1 + 4];
      if (DAT_183a0f758 == '\0') {
        puVar8 = &Method$System.Collections.Generic.Dictionary<Projectile,-TeamQTEResult>.TryGetValue();
        FUN_1802a4cc0(&Method$System.Collections.Generic.Dictionary<Projectile,-TeamQTEResult>.TryGetValue());
        DAT_183a0f758 = '\x01';
      }
      puVar3 = (undefined8 *)param_1[0x36];
      local_res8 = (undefined8 *)0x0;
      if (puVar3 == (undefined8 *)0x0) {
LAB_18053a240:
        FUN_1802845b0(puVar8,param_2,param_3,(char *)param_4);
        pcVar5 = (code *)swi(3);
        (*pcVar5)();
        return;
      }
      param_3 = *(ulonglong *)(*(longlong *)(*(longlong *)(
          Method$..Dictionary<Projectile,-TeamQTEResult>.TryGetValue() + 0x20) + 0xc0) + 0xa0);
      puVar8 = puVar3;
      param_2 = plVar10;
      uVar6 = (**(code **)(param_3 + 8))();
      if ((int)uVar6 < 0) {
        local_res8 = (undefined8 *)0x0;
      }
      else {
        puVar8 = (undefined8 *)puVar3[3];
        if (puVar8 == (undefined8 *)0x0) goto LAB_18053a240;
        if (*(uint *)(puVar8 + 3) <= uVar6) {
          FUN_1802845c0(puVar8,param_2,param_3,(char *)param_4);
          pcVar5 = (code *)swi(3);
          (*pcVar5)();
          return;
        }
        puVar8 = (undefined8 *)puVar8[(longlong)(int)uVar6 * 3 + 6];
        // (write barrier for GC)
        local_res8 = puVar8;
        if (puVar8 == (undefined8 *)0x0) goto LAB_18053a240;
        param_2 = (longlong *)0x0;
        pcVar7 = TeamQTEResult$$GetSuccessCount((undefined *)puVar8);
        if (0 < (int)pcVar7) {
          param_3 = 0;
          puVar8 = param_1;
          PotionKick$$KickPotion((longlong)param_1,plVar10,(ulonglong *)0x0,param_4);
          param_2 = plVar10;
        }
      }
    }
  }
  FUN_1802845b0(puVar8,param_2,param_3,(char *)param_4);
  pcVar5 = (code *)swi(3);
  (*pcVar5)();
  return;
}
```

---

## IL2CPP Parameter Mapping

```c
void PotionKick$$ProcessSuccessfulKicks(
    undefined8 *param_1,   // this (PotionKick*)
    ...
)
```

`param_1` is `undefined8*` — each index step is 8 bytes.

---

## Field Offsets Referenced

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1[0x33]` | `0x198` | `kickablePotions` (List\<Projectile\>) |
| `param_1[0x36]` | `0x1B0` | `qteResultsByPotion` (Dictionary\<Projectile, TeamQTEResult\>) |

**IL2CPP List\<T\> layout** (`kickablePotions`):

| Offset | Field |
|---|---|
| `+0x10` | `_items` (backing array, `Il2CppArray*`) |
| `+0x18` | `_size` (int, = `Count`) |

**IL2CPP Array layout** (`_items`, itself an `Il2CppArraySize` object):

| Offset | Field |
|---|---|
| `+0x00` | klass* (8 bytes) |
| `+0x08` | monitor* (8 bytes) |
| `+0x10` | Il2CppArrayBounds* (8 bytes) |
| `+0x18` | max_length (8 bytes) |
| `+0x20` | elements start → index 4 as `undefined8*` (`puVar8[4]`) |

So `puVar8[(longlong)(int)uVar1 + 4]` reads `_items.elements[uVar1]` — the i-th `Projectile*`.

**IL2CPP Dictionary\<K,V\> internal TryGetValue return convention:**  
Returns the entry *index* (≥ 0 if found, −1 if not). Then the method accesses
`_entries` (at `dict[3]`) inline rather than boxing through a managed `out` parameter.

**IL2CPP Dictionary entry layout** (each `{hashCode+next, key, value}` = 3 × `undefined8`):

```
_entries (Il2CppArray, header = 4 × undefined8):
    elements start at _entries[4]
    entry[i].hashCode/next = _entries[4 + i*3 + 0]
    entry[i].key           = _entries[4 + i*3 + 1]
    entry[i].value         = _entries[4 + i*3 + 2]
```

`puVar8[(int)uVar6 * 3 + 6]` = `_entries[(uVar6*3 + 4 + 2)]` = `entry[uVar6].value` ✓

---

## Logic Walkthrough

### 1. Guard: kickablePotions not null

```c
if (kickablePotions == null) return;  // implicit via if (param_1[0x33] != 0)
```

### 2. Snapshot count and iterate backwards

```c
int count = kickablePotions._size;   // snapshot before loop
for (int i = count - 1; i >= 0; i--)
```

Backwards iteration is safe if `KickPotion` internally calls `RemoveKickablePotion`
(which modifies the list) — iterating in reverse means later indices are unaffected.

### 3. Fetch Projectile at index i

```c
Projectile* items = kickablePotions._items.elements;
Projectile potion = items[i];
```

### 4. Dictionary lookup: find TeamQTEResult for this potion

```c
int entryIndex = qteResultsByPotion.InternalTryGetValue(potion);
// entryIndex == -1 → not found → local_res8 = null
// entryIndex >= 0  → local_res8 = _entries[entryIndex].value
TeamQTEResult teamQTE = local_res8;
```

### 5. Check success count

```c
if (teamQTE.GetSuccessCount() > 0)
{
    KickPotion(potion);
}
```

`TeamQTEResult.GetSuccessCount()` returns how many stored `QTEResult` entries have a
successful result. If any player (SPP or Seraï's seraiKickState) has had
`AddResult(new QTEResult(player, EQTEResult.SuccessPerfect))` called on this potion's
teamQTE (by `UpdateKickForPlayer`), this count is > 0.

---

## Equivalent C#

```csharp
private void ProcessSuccessfulKicks()
{
    // kickablePotions @ 0x198 (param_1[0x33])
    if (kickablePotions == null) return;

    // _size (Count) read directly from List+0x18 — snapshot before loop
    int count = kickablePotions.Count;

    // Backwards iteration — safe if KickPotion calls RemoveKickablePotion mid-loop
    for (int i = count - 1; i >= 0; i--)
    {
        // _items (Il2CppArray*) at List+0x10; elements start at array+0x20 (index 4 as undefined8*)
        // → puVar8[(longlong)(int)uVar1 + 4]
        Projectile potion = kickablePotions[i];

        // qteResultsByPotion @ 0x1B0 (param_1[0x36])
        // IL2CPP TryGetValue returns entry index (≥0) or -1; value read from
        // _entries[entryIndex].value = _entries[(index*3 + 4 + 2)]
        //   → puVar8[(int)uVar6 * 3 + 6]
        if (!qteResultsByPotion.TryGetValue(potion, out TeamQTEResult teamQTE))
            continue;

        // teamQTE.GetSuccessCount() > 0 → at least one QTEResult with success was AddResult'd
        // by UpdateKickForPlayer (proximity or cooperative auto-grant)
        if (teamQTE.GetSuccessCount() > 0)
            KickPotion(potion);   // sole call site for KickPotion in the entire class
    }
}
```

---

## Key Observations

### 1. `KickPotion` is ONLY called from here

`ProcessSuccessfulKicks` is the sole entry point for `KickPotion`. There is no fast-path or
shortcut. Every successful kick — whether from button press, proximity auto-grant, or
cooperative auto-grant — must go through `teamQTE.GetSuccessCount() > 0` in this method.

### 2. `GetSuccessCount() > 0` is the gate — and it requires `UpdateKickForPlayer` to run first

Within a single `UpdateKicks()` call, `UpdateKickForPlayer` runs before `ProcessSuccessfulKicks`.
So the sequence is:
```
UpdateKicks() {
    for each SPP player: UpdateKickForPlayer() → may add QTEResult to teamQTE
    ProcessSuccessfulKicks()           → checks GetSuccessCount() → calls KickPotion
    ProcessOutdatedPotions()
    ProcessPotionsKickedByAllPlayers()
}
```

### 3. Why Attempt 1 (call `OnKick()` from `OnLobPotion` Postfix) failed — REVISED EXPLANATION

The `OnLobPotion` analysis revealed that `kickablePotions.Add(potion)` runs synchronously
**inside** `OnLobPotion`, before the Postfix fires. So `kickablePotions` WAS populated when
`OnKick()` was called. The failure was NOT an empty list.

The real failure was in **`UpdateKickForPlayer`'s proximity check**:

```csharp
Vector3 delta = potion.transform.position - kickImpactPosition;
if (Vector3.Magnitude(delta) > validKickMaxPotionDistance) continue;
```

When the Postfix fires, the potion has been spawned at Seraï's throw **socket** position —
essentially at Seraï's hand. `kickImpactPosition` is somewhere near the enemy (or at the
intermediate landing zone). The distance is large → the proximity check fails → no
`QTEResult` is added → `GetSuccessCount() == 0` → `KickPotion` is never called.

**The auto-timing patch must wait until the potion is physically near `kickImpactPosition`
before invoking `kickCallback.Invoke()`.** Only then will the proximity check pass.

### 4. `kickImpactPosition` is the target, not Seraï's position

From `UpdateKickForPlayer`: `delta = potion.position - kickImpactPosition`. The
proximity check is whether the **flying potion** is near `kickImpactPosition` — not
whether the player character is near anything. As the lob arc progresses, the potion
approaches `kickImpactPosition`, and eventually the proximity condition becomes true.

### 5. The cooperative auto-grant (`HasAPlayerKickedPotion`) is irrelevant for Seraï

`HasAPlayerKickedPotion` returns true if the potion is already in `kickedPotions` (i.e.,
it's already been kicked). For multipler benefit: once Seraï kicks, SPP players get a free
perfect on the next `UpdateKickForPlayer` frame. But Seraï must kick first.

---

## Implications for Auto-Timing

The correct approach is now clear:

> **Wait until `potion.transform.position` is within `validKickMaxPotionDistance` of
> `kickImpactPosition`, then call `kickCallback.Invoke()` (or directly invoke `UpdateKicks`).**

This can be implemented by patching `KickPotionState.StateExecute` (which already reads
`nextKickValidTime` as a cooldown gate) and adding a proximity check before the existing
input detection — if the potion is close enough, fire `kickCallback` automatically without
waiting for a button press.

Alternatively, patch `Update` / `UpdateKicks` directly with a guard that auto-fires once
per potion when proximity is met.

---

## Open Questions

| Question | Where to look |
|---|---|
| What is `kickImpactPosition` set to? | `OnReshanPotionFound` / `DoMove` / `potionReachedSeraiCallback` body |
| What does `KickPotion` do after launching the trajectory? | `PotionKick$$KickPotion` (RVA `0x53B1C0`) |
| Does `RemoveKickablePotion` run *inside* `KickPotion`? | Same RVA — affects whether the backwards list iteration is needed |
| What is `validKickMaxPotionDistance`? | Inspector / config value; determines how large the proximity window is |
