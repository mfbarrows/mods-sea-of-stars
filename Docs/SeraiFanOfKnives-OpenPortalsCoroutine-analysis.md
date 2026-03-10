# `SeraiFanOfKnives.OpenPortalsCoroutine` — Analysis

Method: `SeraiFanOfKnives.<>d__46$$MoveNext`  
VA: `0x1805EB110` (factory) / MoveNext inferred from class TypeDefIndex 1276  
State machine class: `SeraiFanOfKnives.<OpenPortalsCoroutine>d__46`

---

## State machine field map (d__46, from dump.cs)

| Field | Offset | Type | Role |
|---|---|---|---|
| `<>1__state` | `+0x10` | `int` | Current iterator state |
| `<>2__current` | `+0x18` | `object` | Value yielded to coroutine driver |
| `<>4__this` | `+0x20` | `SeraiFanOfKnives*` | Owning move instance |

*(no extra locals stored — all work is done inline each MoveNext call)*

---

## Key field offsets used

`lVar1 = *(longlong*)(param_1 + 0x20)` = the `SeraiFanOfKnives` instance (`<>4__this`)

| Expression | Byte offset | C# field |
|---|---|---|
| `*(longlong*)(lVar1 + 0x98)` | `0x98` | `instructionsLocId` (low 8 bytes of 16-byte LocalizationId) |
| `*(longlong*)(lVar1 + 0xa0)` | `0xA0` | `instructionsLocId` (high 8 bytes) |
| `*(longlong*)(lVar1 + 0x78)` | `0x78` | `playerActor` (PlayerCombatActor / SeraiCombatActor) — inherited from `PlayerCombatMove` |
| `*(playerActor + 0x78)` | `0x78` on CombatActor | `playerActor.dependencies` (CombatActorDependencies) |
| `dependencies[3]` = `*(dependencies + 0x18)` | `+0x18` on CombatActorDependencies | `dependencies.animator` (Animator) |

`playerActor.dependencies` is accessed two ways in this function:
- Via `*(*(lVar1+0x78) + 0x78)` (direct load)
- Via `*(playerActor)[0xf]` = `*(playerActor + 0x78)` — same result, different Ghidra rebasing

---

## Animation hashes used (SeraiAnims, from dump.cs)

| Static field | Static offset | Usage |
|---|---|---|
| `SeraiAnims.OpenHorizontalPortal` | `+0x0C` | Played on portal open; waited on |
| `SeraiAnims.FanOfKnivesIn` | `+0x10` | Played at end (coroutine done) |

---

## State machine flow

```
State 0 (initial entry)
  │
  ├─ if instructionsLocId valid (both halves non-null):
  │   ├─ yield return PlayerCombatMove.ShowInstructions(this, instructionsLocId)  → State 1
  │   └─ return true
  │
  └─ if not valid (no instructions):
      └─▶ fall to [Common]

State 1 (ShowInstructions coroutine has finished)
  │
  └─ set state = -1, fall to [Common]

[Common] (reached from state 0 no-instructions, or state 1 done)
  │
  ├─ animator.Play(SeraiAnims.OpenHorizontalPortal, -1)
  ├─ yield return CoroutineUtil.WaitForAnimationDone(
  │       animator, SeraiAnims.OpenHorizontalPortal)  → State 2
  └─ return true

State 2 (portal-open animation done)
  │
  ├─ animator.Play(SeraiAnims.FanOfKnivesIn, -1)
  └─ return false  (coroutine complete)
```

---

## Original Ghidra C

```c
ulonglong SeraiFanOfKnives.<>d__46$$MoveNext
                    (longlong param_1,ulonglong param_2,char *param_3,longlong ******param_4)

{
  longlong lVar1;
  longlong lVar2;
  ulonglong *puVar3;
  code *pcVar4;
  undefined **in_RAX;
  ulonglong *puVar5;
  undefined1 (*pauVar6) [16];
  undefined8 uVar7;
  uint uVar8;
  longlong *plVar9;
  ulonglong uVar10;
  bool bVar11;
  undefined8 local_18;
  undefined8 uStack_10;
  
  if (DAT_183a0fb1e == '\0') {
    in_RAX = FUN_1802a4cc0((longlong *)&SeraiAnims_TypeInfo);
    DAT_183a0fb1e = '\x01';
  }
  uVar8 = *(uint *)(param_1 + 0x10);
  plVar9 = (longlong *)(ulonglong)uVar8;
  lVar1 = *(longlong *)(param_1 + 0x20);
  if (uVar8 == 0) {
    *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
    if (lVar1 == 0) goto LAB_1805ef3e2;
    if ((((*(longlong *)(lVar1 + 0x98) != 0) && (*(int *)(*(longlong *)(lVar1 + 0x98) + 0x10) != 0))
        && (*(longlong *)(lVar1 + 0xa0) != 0)) &&
       (*(int *)(*(longlong *)(lVar1 + 0xa0) + 0x10) != 0)) {
      local_18 = *(undefined8 *)(lVar1 + 0x98);
      uStack_10 = *(undefined8 *)(lVar1 + 0xa0);
      pauVar6 = PlayerCombatMove$$ShowInstructions(lVar1,&local_18,(undefined *)0x0,param_4);
      *(undefined1 (**) [16])(param_1 + 0x18) = pauVar6;
      uVar7 = FUN_180269b20(param_1 + 0x18);
      *(undefined4 *)(param_1 + 0x10) = 1;
      return CONCAT71((int7)((ulonglong)uVar7 >> 8),1);
    }
  }
  else {
    uVar8 = uVar8 - 1;
    plVar9 = (longlong *)(ulonglong)uVar8;
    if (uVar8 != 0) {
      if (uVar8 != 1) {
LAB_1805ef23a:
        return (ulonglong)in_RAX & 0xffffffffffffff00;
      }
      *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
      if (((lVar1 != 0) && (*(longlong *)(lVar1 + 0x78) != 0)) &&
         (plVar9 = *(longlong **)(*(longlong *)(lVar1 + 0x78) + 0x78), plVar9 != (longlong *)0x0)) {
        lVar1 = plVar9[3];
        if ((int)SeraiAnims_TypeInfo[0x1c] == 0) {
          plVar9 = SeraiAnims_TypeInfo;
          FUN_180310620(SeraiAnims_TypeInfo,param_2,param_3,param_4);
        }
        if (lVar1 != 0) {
          in_RAX = (undefined **)
                   UnityEngine.Animator$$Play
                             (lVar1,*(undefined4 *)(SeraiAnims_TypeInfo[0x17] + 0x10),0xffffffff,
                              (char *)param_4);
          goto LAB_1805ef23a;
        }
      }
      goto LAB_1805ef3e2;
    }
    *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
    if (lVar1 == 0) goto LAB_1805ef3e2;
  }
  if ((*(longlong *)(lVar1 + 0x78) != 0) &&
     (plVar9 = *(longlong **)(*(longlong *)(lVar1 + 0x78) + 0x78), plVar9 != (longlong *)0x0)) {
    lVar2 = plVar9[3];
    if ((int)SeraiAnims_TypeInfo[0x1c] == 0) {
      plVar9 = SeraiAnims_TypeInfo;
      FUN_180310620(SeraiAnims_TypeInfo,param_2,param_3,param_4);
    }
    if (lVar2 != 0) {
      param_3 = (char *)0xffffffff;
      param_2 = (ulonglong)*(uint *)(SeraiAnims_TypeInfo[0x17] + 0xc);
      UnityEngine.Animator$$Play
                (lVar2,*(uint *)(SeraiAnims_TypeInfo[0x17] + 0xc),0xffffffff,(char *)param_4);
      plVar9 = *(longlong **)(lVar1 + 0x78);
      if ((plVar9 != (longlong *)0x0) &&
         (plVar9 = (longlong *)plVar9[0xf], plVar9 != (longlong *)0x0)) {
        puVar5 = CoroutineUtil$$WaitForAnimationDone
                           ((ulonglong *)plVar9[3],
                            (ulonglong)*(uint *)(SeraiAnims_TypeInfo[0x17] + 0xc),(ulonglong *)0x0,
                            (longlong ******)0x0);
        bVar11 = DAT_183a0b2e4 != 0;
        *(undefined8 *)(param_1 + 0x18U) = puVar5;
        if (bVar11) {
          uVar8 = (uint)(param_1 + 0x18U >> 0xc);
          uVar10 = (ulonglong)((uVar8 & 0x1fffff) >> 6);
          do {
            puVar3 = *(ulonglong **)(&DAT_183a6a380 + uVar10 * 8);
            LOCK();
            puVar5 = *(ulonglong **)(&DAT_183a6a380 + uVar10 * 8);
            bVar11 = puVar3 == puVar5;
            if (bVar11) {
              *(ulonglong *)(&DAT_183a6a380 + uVar10 * 8) = (ulonglong)puVar3 | 1L << (uVar8 & 0x3f);
              puVar5 = puVar3;
            }
            UNLOCK();
          } while (!bVar11);
        }
        *(undefined4 *)(param_1 + 0x10) = 2;
        return CONCAT71((int7)((ulonglong)puVar5 >> 8),1);
      }
    }
  }
LAB_1805ef3e2:
  FUN_1802845b0(plVar9,param_2,param_3,(char *)param_4);
  pcVar4 = (code *)swi(3);
  uVar7 = (*pcVar4)();
  return uVar7;
}
```

---

## Reconstructed C#

```csharp
private IEnumerator OpenPortalsCoroutine()
{
    // ── State 0: show instructions if set ────────────────────────────────────
    bool hasInstructions = instructionsLocId.idLow  != 0
                        && instructionsLocId.idHigh != 0;
    if (hasInstructions)
    {
        // PlayerCombatMove.ShowInstructions is inherited; called with a stack-copy
        // of the 16-byte instructionsLocId value type.
        yield return ShowInstructions(instructionsLocId);  // State 1
    }

    // ── [Common] play portal-open animation and wait for it ──────────────────
    // playerActor is PlayerCombatMove.playerActor (+0x78 on PlayerCombatMove).
    // playerActor.dependencies is CombatActor.dependencies (+0x78 on CombatActor).
    // dependencies.animator is CombatActorDependencies.animator (+0x18).
    Animator anim = playerActor.dependencies.animator;
    anim.Play(SeraiAnims.OpenHorizontalPortal, -1);
    yield return CoroutineUtil.WaitForAnimationDone(
        anim, SeraiAnims.OpenHorizontalPortal);  // State 2

    // ── State 2: transition into Fan-of-Knives pose ───────────────────────────
    // Re-fetch animator (Ghidra re-resolves through playerActor[0xf] = dependencies).
    anim = playerActor.dependencies.animator;
    anim.Play(SeraiAnims.FanOfKnivesIn, -1);
    // Coroutine done — control returns to whoever called StartCoroutine(OpenPortalsCoroutine()).
}
```

---

## Confirmed: `PlayerCombatMove.ShowInstructions` IS used here

Unlike `MoonrangSpecialMove`, Fan of Knives delegates entirely to the base:

```c
pauVar6 = PlayerCombatMove$$ShowInstructions(lVar1, &local_18, ...);
*(param_1 + 0x18) = pauVar6;   // <>2__current = the ShowInstructions IEnumerator
*(param_1 + 0x10) = 1;          // <>1__state = 1
return true;                    // MoveNext: keep going
```

The returned `IEnumerator` from `ShowInstructions` is stored as `<>2__current` and driven by
the coroutine engine. When it completes, this coroutine resumes at state 1, which falls through
to the animation block immediately.

---

## Patch: skip instructions for Fan of Knives

Because `ShowInstructions` is the sole blocker, the minimal patch is to return an empty
coroutine from it.  The animation and the rest of `OpenPortalsCoroutine` are **unaffected**
— they run immediately after.

```csharp
/// <summary>
/// Suppress instruction text for all moves that use PlayerCombatMove.ShowInstructions.
/// Confirmed callers: SeraiFanOfKnives (OpenPortalsCoroutine).
/// Also affects: PotionKick, ConflagrateCombatMove, Jugglecore, MoongirlPoleVaultDrop
/// — all use the same base method.
/// </summary>
[HarmonyPatch(typeof(PlayerCombatMove), "ShowInstructions")]
static class Patch_PlayerCombatMove_SkipInstructions
{
    static bool Prefix(ref IEnumerator __result)
    {
        __result = Empty();
        return false;
    }

    static IEnumerator Empty() { yield break; }
}
```

This is safe because `ShowInstructions` is a pure UI method — it shows localised text and waits
for input. Replacing it with `yield break` means `OpenPortalsCoroutine` resumes at state 1
immediately, which falls through to `Play(OpenHorizontalPortal)` on the very next frame.
No state fields on `SeraiFanOfKnives` are written by `ShowInstructions`.

---

## Notes

| Item | Observation |
|---|---|
| `instructionsLocId` null check | Both the low and high 8-byte halves are checked — specifically `*(int*)(half + 0x10)` which is a length/valid field inside the managed string embedded in `LocalizationId`. |
| `FUN_180269b20(param_1 + 0x18)` | IL2CPP incremental GC write-barrier for reference stored at `<>2__current` (+0x18). Standard boilerplate. |
| `FUN_180310620` | IL2CPP lazy class-init for `SeraiAnims` static field block. Runs at most once. |
| `plVar9[0xf]` re-fetch | Ghidra re-loads `playerActor` and resolves `+0x78` again rather than reusing the earlier local. This is a compiler artefact — semantically it's the same `dependencies.animator`. |
| Conflagrate | Also calls `ShowInstructions` (has `instructionsLocId` at `+0x100`). The same patch covers it. |
