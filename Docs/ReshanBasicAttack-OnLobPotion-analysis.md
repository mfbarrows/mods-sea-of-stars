# Analysis: `ReshanBasicAttack$$OnLobPotion`

## Background

`ReshanBasicAttack` is Resh'an's basic attack move. During the move, an attack-hit animation
event fires at the throw frame. `OnLobPotion` is that animation event handler — it is
registered once (during `DoMove`) on `playerActor.animEventHandler.onAttackHit`, executes
once when the animation frame fires, unregisters itself, and delegates the actual work to
`SpawnLobPotion`.

---

## Original Ghidra C

```c
void ReshanBasicAttack$$OnLobPotion
               (undefined1 (*param_1) [16],undefined1 (*param_2) [16],undefined *param_3,
               undefined1 (*param_4) [16])

{
  longlong lVar1;
  code *pcVar2;
  undefined1 (*pauVar3) [16];
  undefined1 (*pauVar4) [16];
  longlong ******pppppplVar5;
  
  pauVar4 = param_1;
  if (DAT_183a0fa8f == '\0') {
    FUN_1802a4cc0((longlong *)&System.Action_TypeInfo);
    pauVar4 = (undefined1 (*) [16])&Method$ReshanBasicAttack.OnLobPotion();
    FUN_1802a4cc0((longlong *)&Method$ReshanBasicAttack.OnLobPotion());
    DAT_183a0fa8f = '\x01';
  }
  if (*(longlong *)(param_1[7] + 8) != 0) {
    lVar1 = *(longlong *)(*(longlong *)(param_1[7] + 8) + 0x100);
    pauVar4 = System.Action_TypeInfo;
    pauVar3 = FUN_1802e0150(*System.Action_TypeInfo,param_2,param_3,(longlong ******)param_4);
    if (pauVar3 != (undefined1 (*) [16])0x0) {
      param_4 = (undefined1 (*) [16])0x0;
      pauVar4 = pauVar3;
      param_2 = param_1;
      param_3 = Method$ReshanBasicAttack.OnLobPotion();
      UnityEngine.RemoteSettings.UpdatedEventHandler$$.ctor
                ((char *)pauVar3,(longlong)param_1,(longlong)Method$ReshanBasicAttack.OnLobPotion())
      ;
      if (lVar1 != 0) {
        pppppplVar5 = (longlong ******)0x0;
        PlayerAnimationEventHandler$$remove_onAttackHit
                  (lVar1,(longlong *)pauVar3,0,(longlong ******)param_4);
        ReshanBasicAttack$$SpawnLobPotion(param_1,(undefined1 (*) [16])0x0,pppppplVar5,param_4);
        return;
      }
    }
  }
  FUN_1802845b0(pauVar4,param_2,param_3,(char *)param_4);
  pcVar2 = (code *)swi(3);
  (*pcVar2)();
  return;
}
```

---

## IL2CPP Parameter Mapping

```c
void ReshanBasicAttack$$OnLobPotion(
    undefined1 (*param_1)[16],   // this (ReshanBasicAttack*)
    ...                          // method info / IL2CPP extras
)
```

`param_1` is typed as a pointer to 16-byte arrays. Field access:
- `param_1[7]` = byte offset 7 × 16 = **0x70** → `PlayerCombatMove.moveDefinition`... 

**Wait — pointer arithmetic correction:**  
`param_1[7]` advances the *pointer* by 7 elements and then evaluates the element at that
address (i.e., reads 16 bytes from offset 0x70). But `(param_1[7] + 8)` adds 8 to those
bytes, meaning Ghidra is reading the 8-byte value at offset 0x70 as a pointer, then
dereferencing at +8.

Byte offset `0x70` on `ReshanBasicAttack` falls in inherited `PlayerCombatMove` fields:

| Offset | Type | C# field |
|---|---|---|
| `0x70` | `PlayerCombatMoveDefinition` | `PlayerCombatMove.moveDefinition` |
| `0x78` | `PlayerCombatActor` | `PlayerCombatMove.playerActor` |

`param_1[7] + 8` reads 8 bytes at **0x70 + 8 = 0x78** = `playerActor`
(PlayerCombatActor). Then `*(longlong *)(playerActor + 0x100)` reads
`PlayerCombatActor.animEventHandler` (offset `0x100`).

So: `lVar1 = this.playerActor.animEventHandler`

---

## Logic Walkthrough

### 1. One-time IL2CPP method cache init

```c
if (DAT_183a0fa8f == '\0') {
    FUN_1802a4cc0(&System.Action_TypeInfo);
    FUN_1802a4cc0(&Method$ReshanBasicAttack.OnLobPotion());
    DAT_183a0fa8f = 1;
}
```

Standard IL2CPP lazy-init pattern for method info pointers.

### 2. Navigate to animEventHandler

```c
lVar1 = playerActor.animEventHandler;   // PlayerCombatActor @ 0x78, animEventHandler @ 0x100
```

### 3. Allocate a new Action delegate wrapping this method

```c
pauVar3 = FUN_1802e0150(System.Action_TypeInfo, ...);
// FUN_1802e0150 = il2cpp_object_new(type) + initialize delegate
new Action(target: this, method: ReshanBasicAttack.OnLobPotion)
```

### 4. Unsubscribe self from onAttackHit

```c
PlayerAnimationEventHandler$$remove_onAttackHit(lVar1, pauVar3, ...);
```

Removes the freshly-created delegate from `animEventHandler.onAttackHit`. This is the
**one-shot unsubscription** pattern: the method was registered as the attack-hit handler;
now that it is running, it immediately removes itself to prevent re-firing on future attacks.

### 5. Delegate actual spawn work to SpawnLobPotion

```c
ReshanBasicAttack$$SpawnLobPotion(param_1, ...);
```

All projectile instantiation, trajectory setup, and callback wiring happens in `SpawnLobPotion`.

---

## Equivalent C#

```csharp
private void OnLobPotion()
{
    var animHandler = playerActor?.animEventHandler;
    if (animHandler == null) return;

    // One-shot: unsubscribe self, then do the work
    animHandler.onAttackHit -= OnLobPotion;
    SpawnLobPotion();
}
```

---

## Key Observations

1. **Pure one-shot delegate management.** This method contains no gameplay logic — it exists
   solely to handle the "run once on attack-hit animation frame, then never again" pattern.

2. **`SpawnLobPotion` is where all the real work happens.** To understand projectile
   spawning, `kickablePotions` population, and callback registration for Resh'an's basic
   attack, decompile `SpawnLobPotion` (RVA `0x5D2CA0`).

3. **Contrast with `PotionKick.OnLobPotion`:** The PotionKick version does everything
   inline (spawn, position, trajectory, list population, QTE registration). The basic attack
   version delegates all of that to `SpawnLobPotion`, keeping `OnLobPotion` as a thin
   one-shot adapter.
