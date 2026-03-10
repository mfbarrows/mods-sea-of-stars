# `Soonrang.ShowInstructionsAndThrowMoonrangCoroutine` — Analysis

Method: `Soonrang.<>d__41$$MoveNext`  
VA (MoveNext): `0x180590D80`  
Factory VA: `0x18058C070` (Slot 59 — overrides `MoonrangSpecialMove`)

---

## State machine field map (d__41, from dump.cs)

| Field | Offset | Type | Role |
|---|---|---|---|
| `<>1__state` | `+0x10` | `int` | Current iterator state |
| `<>2__current` | `+0x18` | `object` | Value yielded to coroutine driver |
| `<>4__this` | `+0x20` | `Soonrang*` | Owning move instance |

**Note:** No `<typingTimeLeft>`, no `<battleScreen>` — the absence of these locals
from the compiler-generated class confirms that this override contains no instruction
UI logic whatsoever.

---

## State machine flow

```
State 0 (initial entry)
  │
  ├─ set state = -1
  ├─ ThrowMoonrang()   ← virtual call, klass vtable slot 60 (klass+0x4F8)
  ├─ <>2__current = null
  ├─ state = 1
  └─▶ return true  (yield return null — one frame)

State 1
  │
  ├─ set state = -1
  └─▶ return false  (coroutine done)

Any other state
  └─▶ return false immediately
```

---

## Original Ghidra C

```c
undefined8
Soonrang.<>d__41$$MoveNext(longlong param_1,undefined8 param_2,undefined8 param_3,char *param_4)

{
  ulonglong *puVar1;
  int iVar2;
  longlong *plVar3;
  ulonglong uVar4;
  code *pcVar5;
  ulonglong uVar6;
  undefined8 uVar7;
  uint uVar8;
  bool bVar9;

  iVar2 = *(int *)(param_1 + 0x10);
  if (iVar2 != 0) {
    if (iVar2 == 1) {
      *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
    }
    return (ulonglong)(uint3)((uint)iVar2 >> 8) << 8;
  }
  *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
  plVar3 = *(longlong **)(param_1 + 0x20);
  if (plVar3 == (longlong *)0x0) {
    FUN_1802845b0(0,param_2,param_3,param_4);
    pcVar5 = (code *)swi(3);
    uVar7 = (*pcVar5)();
    return uVar7;
  }
  uVar6 = (**(code **)(*plVar3 + 0x4f8))(plVar3,*(undefined8 *)(*plVar3 + 0x500));
  bVar9 = DAT_183a0b2e4 != 0;
  *(undefined8 *)(param_1 + 0x18) = 0;
  if (bVar9) {
    uVar8 = (uint)((ulonglong)(param_1 + 0x18) >> 0xc);
    puVar1 = (ulonglong *)(&DAT_183a6a380 + (ulonglong)((uVar8 & 0x1fffff) >> 6) * 8);
    do {
      uVar4 = *puVar1;
      LOCK();
      uVar6 = *puVar1;
      bVar9 = uVar4 == uVar6;
      if (bVar9) {
        *puVar1 = uVar4 | 1L << (uVar8 & 0x3f);
        uVar6 = uVar4;
      }
      UNLOCK();
    } while (!bVar9);
  }
  *(undefined4 *)(param_1 + 0x10) = 1;
  return CONCAT71((int7)(uVar6 >> 8),1);
}
```

---

## Reconstructed C#

```csharp
protected override IEnumerator ShowInstructionsAndThrowMoonrangCoroutine()
{
    ThrowMoonrang();   // virtual call, vtable slot 60 — Soonrang's own override
    yield return null; // one-frame pause before coroutine completes
}
```

---

## Key findings

### No instructions, no animation wait

Soonrang's override:
- Calls `ThrowMoonrang()` **immediately** on the first frame
- Yields `null` (one-frame pause) — likely to ensure the throw animation has started before
  the coroutine machinery finishes
- Done

There is **no** `ShowInstructions`, no `TypeText`, no `InputCategory.GetButton` poll,
no `WaitForAnimationDone`. Soonrang never shows an instruction screen.

### Vtable call confirmed

`(**(code**)(*plVar3 + 0x4F8))(plVar3, *(undefined8*)(*plVar3 + 0x500))`  
= virtual dispatch at slot 60, same as `MoonrangSpecialMove.d__38.MoveNext` state 2.  
Since `plVar3` is a `Soonrang*`, this resolves to `Soonrang.ThrowMoonrang` (overridden at
slot 60), not the base `MoonrangSpecialMove.ThrowMoonrang`.

---

## Patch implications

**No patch required for Soonrang.** Its override already skips all instruction UI.

The `[HarmonyPatch(typeof(Soonrang), "ShowInstructionsAndThrowMoonrangCoroutine")]` stub
that was tentatively planned can be omitted entirely.

Only `MoonrangSpecialMove.ShowInstructionsAndThrowMoonrangCoroutine` needs patching —
see `MoonrangSpecialMove-ShowInstructionsAndThrowMoonrangCoroutine-analysis.md`.
