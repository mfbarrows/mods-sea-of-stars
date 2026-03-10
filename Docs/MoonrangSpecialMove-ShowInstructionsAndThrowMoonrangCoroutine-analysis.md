# `MoonrangSpecialMove.ShowInstructionsAndThrowMoonrangCoroutine` — Analysis

Covers two methods:

1. **Factory** — `MoonrangSpecialMove$$ShowInstructionsAndThrowMoonrangCoroutine`  
   (VA `0x1805C5E00`, Slot 59) — allocates the `d__38` state-machine object and returns it; contains no coroutine logic.

2. **State machine body** — `MoonrangSpecialMove.<>d__38$$MoveNext`  
   (VA `0x1805C8F50`) — all coroutine logic lives here.

---

## State machine field map (d__38, from dump.cs)

| Field | Offset | Type | Role |
|---|---|---|---|
| `<>1__state` | `+0x10` | `int` | Current iterator state (-1 = done, 0 = initial, 1 = loop, 2 = post-anim) |
| `<>2__current` | `+0x18` | `object` | Value yielded to the coroutine driver |
| `<>4__this` | `+0x20` | `MoonrangSpecialMove*` | Owning move instance |
| `<typingTimeLeft>5__2` | `+0x28` | `float` | Countdown: clip length, decremented each frame |
| `<battleScreen>5__3` | `+0x30` | `BattleScreen*` | UI panel; non-null only while instructions are on screen |

---

## Key field offsets used (MoonrangSpecialMove = `plVar1`)

`plVar1 = *(longlong**)(param_1 + 0x20)` = the `MoonrangSpecialMove` instance (`<>4__this`)

| C expression | Byte offset | C# field |
|---|---|---|
| `plVar1[0x1b]` | `0xD8` | `instructionsLocId` (low 8 bytes of 16-byte LocalizationId) |
| `plVar1[0x1c]` | `0xE0` | `instructionsLocId` (high 8 bytes) |
| `plVar1[0x20]` | `0x100` | `moongirl` (MoongirlCombatActor*) |
| `*(uint*)(plVar1+0x21)` | `0x108` | `firstThrowLookAngle` (int) |

### CombatActorDependencies fields (at `moongirl.dependencies` = `moongirl+0x78`)

| Offset on dependencies | C# field | Usage |
|---|---|---|
| `+0x18` | `animator` (Animator) | Play animation; call WaitForAnimationDone |
| `+0x38` | `lookDirectionController` (LookDirectionController) | Orient moongirl to face throw direction |

`CombatActor.dependencies` is at `+0x78` on every `CombatActor` (confirmed from dump.cs).

### PlayerCombatActor fields used

| Offset | C# field | Usage |
|---|---|---|
| `+0x1A0` | `player` (Rewired.Player) | Passed to TextTyper; for controller lookup |
| `+0x1A8` | `playerInputs` (PlayerInputs / InputCategory) | Polled via `GetButton()` to detect confirm press |

---

## Animation hashes used (from `MoongirlAnims`, confirmed dump.cs)

| Static field | Static offset | Hash usage |
|---|---|---|
| `MoongirlAnims.ThrowMoonrangIn` | `+0x10` | Played on entry via `Animator.Play()` |
| `MoongirlAnims.ThrowMoonrangLoop` | `+0x14` | Waited on via `CoroutineUtil.WaitForAnimationDone()` |

---

## BattleScreen fields used (at `<battleScreen>5__3`)

| Expression | Offset | Role |
|---|---|---|
| `*(battleScreen + 0x80)` | `+0x80` | Instruction panel component (holds text + dismiss anim) |
| `*(panel + 0x20)` | `+0x20` on panel | `TextTyper` instance — receives `TypeText()` call |

`GameMenuNewCharacterSection$$ShowStatInfos` is called on `battleScreen` directly (Ghidra
names inherited/interface-dispatched methods by the defining type).  
`WorldMapLevelName$$OnOutDone` is called on the panel (at `battleScreen+0x80`) to dismiss it
with an exit animation; same Ghidra symbol-resolution quirk for a shared base/interface method.

---

## State machine flow

```
State 0 (initial entry)
  │
  ├─ Orient moongirl.dependencies.lookDirectionController to firstThrowLookAngle
  │   (only if controller is not currently executing a turn: field+0x28 == false && field+0x2c <= 0)
  │
  ├─ Play MoongirlAnims.ThrowMoonrangIn on moongirl.dependencies.animator
  │
  ├─ Read current clip duration → typingTimeLeft
  │
  ├─ if instructionsLocId is valid:
  │   │  battleScreen = UIManager.Instance.GetView<BattleScreen>()
  │   │  battleScreen.ShowStatInfos()
  │   │  player     = moongirl.Player
  │   │  controller = player.controllers.GetLastActiveController()
  │   │  battleScreen.panel.TypeText(instructionsLocId, 0x3d23d70a, player, controller)
  │   │
  │   └─▶ yield return null  ──────────────────────────→ State 1 (loop)
  │
  └─ if no instructionsLocId:
      └─▶ yield WaitForAnimationDone(animator, ThrowMoonrangLoop) → State 2

State 1 (loop — "wait for confirm")
  │
  ├─ typingTimeLeft -= Time.deltaTime
  │
  ├─ if typingTimeLeft <= 0f:
  │   └─ if moongirl.playerInputs.GetButton(StringLiteral_1581):   // "Attack" or "Interact"
  │       ├─ battleScreen.panel.OnOutDone()   // dismiss instruction text
  │       ├─ battleScreen = null
  │       └─▶ yield WaitForAnimationDone(animator, ThrowMoonrangLoop) → State 2
  │
  └─ else (timer still running, or no button press):
      └─▶ yield return null  ──────────────────────────→ State 1 (stay)

State 2 (post-WaitForAnimationDone)
  │
  └─ ThrowMoonrang()   ← virtual call, klass vtable slot 60 at klass+0x4F8
      └─▶ return false  (coroutine done)
```

**Vtable verification:** slot 60 × 16 bytes/slot + 0x138 vtable-array start in Il2CppClass = `0x138 + 0x3C0 = 0x4F8` ✓ (confirmed from decompiled call `*(*plVar1 + 0x4F8)`).

---

## Original Ghidra C

### Factory (`MoonrangSpecialMove$$ShowInstructionsAndThrowMoonrangCoroutine`)

```c
undefined1 (*) [16]
MoonrangSpecialMove$$ShowInstructionsAndThrowMoonrangCoroutine
          (undefined8 param_1,undefined8 param_2,undefined *param_3,longlong ******param_4)
{
  ulonglong *puVar1;
  ulonglong uVar2;
  ulonglong uVar3;
  code *pcVar4;
  undefined1 (*pauVar5) [16];
  undefined *puVar6;
  uint uVar7;
  bool bVar8;

  if (DAT_183a0fa3b == '\0') {
    FUN_1802a4cc0((longlong *)
                  &MoonrangSpecialMove.<ShowInstructionsAndThrowMoonrangCoroutine>d__38_TypeInfo);
    DAT_183a0fa3b = '\x01';
  }
  puVar6 = MoonrangSpecialMove.<ShowInstructionsAndThrowMoonrangCoroutine>d__38_TypeInfo;
  pauVar5 = FUN_1802e0150(MoonrangSpecialMove.<ShowInstructionsAndThrowMoonrangCoroutine>d__38_TypeI nfo
                          ,param_2,param_3,param_4);
  if (pauVar5 == (undefined1 (*) [16])0x0) {
    FUN_1802845b0(puVar6,param_2,param_3,(char *)0x0);
    pcVar4 = (code *)swi(3);
    pauVar5 = (undefined1 (*) [16])(*pcVar4)();
    return pauVar5;
  }
  bVar8 = DAT_183a0b2e4 != 0;
  *(undefined8 *)pauVar5[2] = param_1;
  *(undefined4 *)pauVar5[1] = 0;
  if (bVar8) {
    uVar7 = (uint)((ulonglong)(pauVar5 + 2) >> 0xc);
    puVar1 = (ulonglong *)(&DAT_183a6a380 + (ulonglong)((uVar7 & 0x1fffff) >> 6) * 8);
    do {
      uVar3 = *puVar1;
      LOCK();
      uVar2 = *puVar1;
      if (uVar3 == uVar2) {
        *puVar1 = uVar3 | 1L << (uVar7 & 0x3f);
      }
      UNLOCK();
    } while (uVar3 != uVar2);
  }
  return pauVar5;
}
```

### MoveNext body (`MoonrangSpecialMove.<>d__38$$MoveNext`)

```c
undefined8
MoonrangSpecialMove.<>d__38$$MoveNext
          (longlong param_1,longlong ******param_2,ulonglong *param_3,longlong ******param_4)

{
  longlong *plVar1;
  longlong *****ppppplVar2;
  longlong lVar3;
  undefined **in_RAX;
  undefined8 *puVar4;
  longlong lVar5;
  code *pcVar6;
  ulonglong *puVar7;
  longlong lVar8;
  undefined8 uVar9;
  uint uVar10;
  char *pcVar11;
  ulonglong *puVar12;
  ulonglong uVar13;
  longlong ******pppppplVar14;
  bool bVar15;
  float extraout_XMM0_Da;
  float fVar16;
  longlong *****local_48;
  undefined4 local_40;
  undefined8 local_38 [2];
  longlong *****local_28;
  longlong lStack_20;

  if (DAT_183a0fa4b == '\0') {
    FUN_1802a4cc0(&Method$Manager<UIManager>.get_Instance());
    FUN_1802a4cc0(&Method$Manager<CombatManager>.get_Instance());
    FUN_1802a4cc0((longlong *)&MoongirlAnims_TypeInfo);
    in_RAX = FUN_1802a4cc0((longlong *)&Method$UIManager.GetView<BattleScreen>());
    DAT_183a0fa4b = '\x01';
  }
  uVar10 = *(uint *)(param_1 + 0x10);
  pcVar11 = (char *)(ulonglong)uVar10;
  plVar1 = *(longlong **)(param_1 + 0x20);
  pppppplVar14 = param_4;
  if (uVar10 == 0) {
    *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
    if (((plVar1 == (longlong *)0x0) || (plVar1[0x20] == 0)) ||
       (lVar8 = *(longlong *)(plVar1[0x20] + 0x78), lVar8 == 0)) goto LAB_1805c947e;
    puVar7 = *(ulonglong **)(lVar8 + 0x38);
    uVar10 = *(uint *)(plVar1 + 0x21);
    param_2 = (longlong ******)(ulonglong)uVar10;
    if (puVar7 == (ulonglong *)0x0) goto LAB_1805c947e;
    if (((char)puVar7[5] == '\0') && ((int)*(uint *)((longlong)puVar7 + 0x2c) < 1)) {
      uVar13 = puVar7[3];
      if (uVar13 != 0) {
        *(undefined1 *)(uVar13 + 0x1c) = 0;
        *(uint *)(uVar13 + 0x18) = uVar10;
        *(undefined1 *)(uVar13 + 0xb0) = 1;
      }
      uVar9 = 0;
      puVar4 = LookDirectionController$$CharacterAngleToDirection3D(local_38,uVar10);
      pppppplVar14 = (longlong ******)0x0;
      param_3 = (ulonglong *)CONCAT71((int7)((ulonglong)uVar9 >> 8),1);
      param_2 = &local_48;
      local_48 = (longlong *****)*puVar4;
      local_40 = *(undefined4 *)(puVar4 + 1);
      LookDirectionController$$SetAngle((uint *)puVar7,(longlong *)param_2,'\x01',0);
      pcVar11 = (char *)puVar7;
    }
    if ((plVar1[0x20] == 0) ||
       (pcVar11 = *(char **)(plVar1[0x20] + 0x78), (ulonglong *)pcVar11 == (ulonglong *)0x0))
    goto LAB_1805c947e;
    puVar7 = *(ulonglong **)((longlong)pcVar11 + 0x18);
    if ((uint)MoongirlAnims_TypeInfo[0x1c] == 0) {
      pcVar11 = (char *)MoongirlAnims_TypeInfo;
      FUN_180310620((longlong *)MoongirlAnims_TypeInfo,param_2,(char *)param_3,pppppplVar14);
    }
    if (puVar7 == (ulonglong *)0x0) goto LAB_1805c947e;
    param_3 = (ulonglong *)0xffffffff;
    param_2 = (longlong ******)(ulonglong)*(uint *)(MoongirlAnims_TypeInfo[0x17] + 0x10);
    UnityEngine.Animator$$Play
              (puVar7,*(uint *)(MoongirlAnims_TypeInfo[0x17] + 0x10),0xffffffff,(char *)pppppplVar14
              );
    lVar8 = FUN_1800127d0(puVar7,param_2,param_3,(ulonglong *)pppppplVar14);
    pcVar11 = (char *)puVar7;
    if ((lVar8 == 0) || (*(longlong *)(lVar8 + 0xe0) == 0)) goto LAB_1805c947e;
    *(undefined4 *)(param_1 + 0x28) = *(undefined4 *)(*(longlong *)(lVar8 + 0xe0) + 0xa0);
    if ((plVar1[0x1b] != 0) &&
       (((*(int *)(plVar1[0x1b] + 0x10) != 0 && (plVar1[0x1c] != 0)) &&
        (*(int *)(plVar1[0x1c] + 0x10) != 0)))) {
      lVar8 = FUN_1800124d0(puVar7,param_2,param_3,(ulonglong *)pppppplVar14);
      pcVar11 = (char *)puVar7;
      if (lVar8 == 0) goto LAB_1805c947e;
      param_2 = Method$UIManager.GetView<BattleScreen>();
      uVar9 = UIManager$$GetView<object>
                        (lVar8,(longlong)Method$UIManager.GetView<BattleScreen>(),(char *)param_3,
                         pppppplVar14);
      *(undefined8 *)(param_1 + 0x30) = uVar9;
      FUN_180269b20(param_1 + 0x30);
      pcVar11 = *(char **)(param_1 + 0x30);
      if ((ulonglong *)pcVar11 == (ulonglong *)0x0) goto LAB_1805c947e;
      param_2 = (longlong ******)0x0;
      GameMenuNewCharacterSection$$ShowStatInfos((longlong)pcVar11,0,param_3,(char *)pppppplVar14);
      if (*(longlong *)(param_1 + 0x30) == 0) goto LAB_1805c947e;
      pcVar11 = (char *)plVar1[0x20];
      lVar8 = *(longlong *)(*(longlong *)(param_1 + 0x30) + 0x80);
      ppppplVar2 = (longlong *****)plVar1[0x1b];
      lVar3 = plVar1[0x1c];
      if ((ulonglong *)pcVar11 == (ulonglong *)0x0) goto LAB_1805c947e;
      param_2 = (longlong ******)0x0;
      param_4 = (longlong ******)
                PlayerCombatActor$$get_Player
                          ((undefined8 *)pcVar11,0,param_3,(ulonglong *)pppppplVar14);
      pcVar11 = (char *)plVar1[0x20];
      if ((ulonglong *)pcVar11 == (ulonglong *)0x0) goto LAB_1805c947e;
      param_2 = (longlong ******)0x0;
      lVar5 = PlayerCombatActor$$get_Player
                        ((undefined8 *)pcVar11,0,param_3,(ulonglong *)pppppplVar14);
      if ((lVar5 == 0) ||
         (pcVar11 = *(char **)(lVar5 + 0x38), (ulonglong *)pcVar11 == (ulonglong *)0x0))
      goto LAB_1805c947e;
      param_2 = (longlong ******)0x0;
      puVar7 = (ulonglong *)
               Rewired.Player.ControllerHelper$$GetLastActiveController
                         ((longlong)pcVar11,0,(char *)param_3,pppppplVar14);
      if ((lVar8 == 0) ||
         (pcVar11 = *(char **)(lVar8 + 0x20), (ulonglong *)pcVar11 == (ulonglong *)0x0))
      goto LAB_1805c947e;
      param_2 = &local_28;
      local_28 = ppppplVar2;
      lStack_20 = lVar3;
      TextTyper$$TypeText((ulonglong *)pcVar11,param_2,0x3d23d70a,param_4,puVar7);
      goto LAB_1805c924a;
    }
LAB_1805c931e:
    pcVar11 = (char *)puVar7;
    if ((plVar1[0x20] == 0) ||
       (pcVar11 = *(char **)(plVar1[0x20] + 0x78), (ulonglong *)pcVar11 == (ulonglong *)0x0)) {
LAB_1805c947e:
      FUN_1802845b0(pcVar11,param_2,param_3,(char *)pppppplVar14);
      pcVar6 = (code *)swi(3);
      uVar9 = (*pcVar6)();
      return uVar9;
    }
    puVar7 = *(ulonglong **)((longlong)pcVar11 + 0x18);
    if ((uint)MoongirlAnims_TypeInfo[0x1c] == 0) {
      FUN_180310620((longlong *)MoongirlAnims_TypeInfo,param_2,(char *)param_3,pppppplVar14);
    }
    puVar7 = CoroutineUtil$$WaitForAnimationDone
                       (puVar7,(ulonglong)*(uint *)(MoongirlAnims_TypeInfo[0x17] + 0x14),
                        (ulonglong *)0x0,(longlong ******)0x0);
    bVar15 = DAT_183a0b2e4 != 0;
    *(undefined8 *)(param_1 + 0x18U) = puVar7;
    if (bVar15) {
      uVar10 = (uint)(param_1 + 0x18U >> 0xc);
      uVar13 = (ulonglong)((uVar10 & 0x1fffff) >> 6);
      do {
        puVar12 = *(ulonglong **)(&DAT_183a6a380 + uVar13 * 8);
        LOCK();
        puVar7 = *(ulonglong **)(&DAT_183a6a380 + uVar13 * 8);
        bVar15 = puVar12 == puVar7;
        if (bVar15) {
          *(ulonglong *)(&DAT_183a6a380 + uVar13 * 8) = (ulonglong)puVar12 | 1L << (uVar10 & 0x3f);
          puVar7 = puVar12;
        }
        UNLOCK();
      } while (!bVar15);
    }
    *(undefined4 *)(param_1 + 0x10) = 2;
  }
  else {
    uVar10 = uVar10 - 1;
    pcVar11 = (char *)(ulonglong)uVar10;
    if (uVar10 != 0) {
      if (uVar10 == 1) {
        *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
        if (plVar1 == (longlong *)0x0) goto LAB_1805c947e;
        in_RAX = (undefined **)
                 (**(code **)(*plVar1 + 0x4f8))(plVar1,*(undefined8 *)(*plVar1 + 0x500));
      }
      return (ulonglong)in_RAX & 0xffffffffffffff00;
    }
    *(undefined4 *)(param_1 + 0x10) = 0xffffffff;
LAB_1805c924a:
    fVar16 = *(float *)(param_1 + 0x28);
    pppppplVar14 = param_4;
    pcVar6 = DAT_183a201d8;
    if (DAT_183a201d8 == (code *)0x0) {
      pcVar11 = "UnityEngine.Time::get_deltaTime()";
      pcVar6 = (code *)FUN_18029a8a0((undefined8 *)"UnityEngine.Time::get_deltaTime()");
      pppppplVar14 = param_4;
      if (pcVar6 == (code *)0x0) {
        lVar8 = FUN_18029a500((byte *)"UnityEngine.Time::get_deltaTime()");
        FUN_1802986e0(lVar8,0,param_3,(char *)param_4);
        pcVar6 = (code *)swi(3);
        uVar9 = (*pcVar6)();
        return uVar9;
      }
    }
    DAT_183a201d8 = pcVar6;
    puVar7 = (ulonglong *)(*DAT_183a201d8)();
    fVar16 = fVar16 - extraout_XMM0_Da;
    *(float *)(param_1 + 0x28) = fVar16;
    if (fVar16 <= 0.0) {
      if (((plVar1 == (longlong *)0x0) || (plVar1[0x20] == 0)) ||
         (puVar12 = *(ulonglong **)(plVar1[0x20] + 0x1a8), puVar12 == (ulonglong *)0x0))
      goto LAB_1805c947e;
      if (DAT_183a12423 == '\0') {
        FUN_1802a4cc0((longlong *)&StringLiteral_1581);
        DAT_183a12423 = '\x01';
      }
      param_3 = (ulonglong *)0x0;
      param_2 = StringLiteral_1581;
      puVar7 = (ulonglong *)
               InputCategory$$GetButton
                         (puVar12,(longlong *)StringLiteral_1581,(ulonglong *)0x0,
                          (ulonglong *)pppppplVar14);
      if ((char)puVar7 != '\0') {
        pcVar11 = (char *)puVar12;
        if ((*(longlong *)(param_1 + 0x30) == 0) ||
           (pcVar11 = *(char **)(*(longlong *)(param_1 + 0x30) + 0x80),
           (ulonglong *)pcVar11 == (ulonglong *)0x0)) goto LAB_1805c947e;
        param_2 = (longlong ******)0x0;
        WorldMapLevelName$$OnOutDone(pcVar11,0,param_3,(char *)pppppplVar14);
        puVar7 = (ulonglong *)(param_1 + 0x30);
        *(undefined8 *)(param_1 + 0x30) = 0;
        FUN_180269b20((ulonglong)puVar7);
        goto LAB_1805c931e;
      }
    }
    bVar15 = DAT_183a0b2e4 != 0;
    *(undefined8 *)(param_1 + 0x18) = 0;
    if (bVar15) {
      uVar10 = (uint)((ulonglong)(param_1 + 0x18) >> 0xc);
      uVar13 = (ulonglong)((uVar10 & 0x1fffff) >> 6);
      do {
        puVar12 = *(ulonglong **)(&DAT_183a6a380 + uVar13 * 8);
        LOCK();
        puVar7 = *(ulonglong **)(&DAT_183a6a380 + uVar13 * 8);
        bVar15 = puVar12 == puVar7;
        if (bVar15) {
          *(ulonglong *)(&DAT_183a6a380 + uVar13 * 8) = (ulonglong)puVar12 | 1L << (uVar10 & 0x3f);
          puVar7 = puVar12;
        }
        UNLOCK();
      } while (!bVar15);
    }
    *(undefined4 *)(param_1 + 0x10) = 1;
  }
  return CONCAT71((int7)((ulonglong)puVar7 >> 8),1);
}
```

---

## Reconstructed C#

```csharp
// ── Factory (the method the C# compiler generates) ────────────────────────────
protected virtual IEnumerator ShowInstructionsAndThrowMoonrangCoroutine()
{
    // IL2CPP allocates a d__38 instance, sets <>1__state=0 and <>4__this=this,
    // marks the GC card table for the reference write, then returns the IEnumerator.
    // No coroutine logic here; all of it is in MoveNext below.
    return new d__38(0) { __4__this = this };
}

// ── MoveNext (the actual coroutine body) ───────────────────────────────────────
private bool MoveNext()  // on d__38
{
    MoonrangSpecialMove @this = <>4__this;

    switch (<>1__state)
    {
        // ── State 0: initial entry ────────────────────────────────────────────
        case 0:
        {
            <>1__state = -1;

            CombatActorDependencies deps = @this.moongirl.dependencies;  // moongirl+0x78
            // Guard: moongirl and dependencies must not be null.

            // ── 1. Orient moongirl to face the initial throw angle ─────────────
            LookDirectionController look = deps.lookDirectionController;  // deps+0x38
            // Only set angle immediately if the controller is currently idle
            // (look.field0x28 == false  &&  look.field0x2c <= 0).
            if (look.field0x28 == false && look.field0x2c <= 0)
            {
                // Pre-configure angle target sub-object (LookDirectionController+0x18):
                var sub = look.field0x18;
                if (sub != null)
                {
                    sub.field0x1c = false;
                    sub.field0x18 = @this.firstThrowLookAngle;  // uint angle
                    sub.field0xb0 = true;
                }
                Vector3 dir = LookDirectionController.CharacterAngleToDirection3D(@this.firstThrowLookAngle);
                look.SetAngle(dir, immediate: true);
            }

            // ── 2. Play ThrowMoonrangIn, capture clip length ──────────────────
            Animator anim = deps.animator;  // deps+0x18
            anim.Play(MoongirlAnims.ThrowMoonrangIn, -1);  // hash at MoongirlAnims static+0x10

            // FUN_1800127d0 is an unknown accessor that returns an object from which
            // the current animation clip length is read at result+0xe0 → float+0xa0.
            // Semantically: typingTimeLeft = current animation clip length.
            <typingTimeLeft>5__2 = /* anim.clip.length, via FUN_1800127d0 */;

            // ── 3. Show instructions UI if instructionsLocId is valid ──────────
            bool hasInstructions = @this.instructionsLocId.idLow  != 0
                                && @this.instructionsLocId.idHigh != 0;
            if (hasInstructions)
            {
                UIManager ui          = Manager<UIManager>.Instance;
                BattleScreen screen   = ui.GetView<BattleScreen>();
                <battleScreen>5__3    = screen;

                screen.ShowStatInfos();  // GameMenuNewCharacterSection$$ShowStatInfos

                // battleScreen+0x80 = instruction panel; panel+0x20 = TextTyper
                object panel    = screen.field0x80;
                TextTyper typer = panel.field0x20;

                Player player           = @this.moongirl.Player;       // PlayerCombatActor$$get_Player
                Controller controller   = player.controllers.GetLastActiveController();

                typer.TypeText(@this.instructionsLocId, /*hash*/ 0x3d23d70a, player, controller);

                // Fall through to the countdown (don't yield here — first frame begins below)
                goto countdown;
            }

            // ── No instructions: skip straight to waiting for animation ────────
            goto waitAnim;
        }

        // ── State 1: countdown + button-wait loop ─────────────────────────────
        case 1:
        {
            <>1__state = -1;
            countdown:

            <typingTimeLeft>5__2 -= Time.deltaTime;

            if (<typingTimeLeft>5__2 <= 0f)
            {
                // moongirl.playerInputs is at PlayerCombatActor+0x1A8
                InputCategory inputs = @this.moongirl.playerInputs;
                bool pressed = inputs.GetButton(StringLiteral_1581);  // probably "Attack"/"Interact"
                if (pressed)
                {
                    // Dismiss the instruction text panel (exit animation)
                    object panel = <battleScreen>5__3.field0x80;
                    panel.OnOutDone();  // WorldMapLevelName$$OnOutDone — inherited/shared dismiss method
                    <battleScreen>5__3 = null;

                    goto waitAnim;
                }
            }

            // Timer still running or button not yet pressed — yield one frame.
            <>2__current = null;
            <>1__state   = 1;
            return true;   // MoveNext returns true = keep going
        }

        // ── State 2: animation done, execute the throw ─────────────────────────
        case 2:
        {
            <>1__state = -1;
            @this.ThrowMoonrang();   // virtual call, vtable slot 60 (klass+0x4F8)
            return false;  // MoveNext returns false = coroutine complete
        }
    }

    // Unreachable in normal operation (state -1 or unknown).
    return false;

    // ── Shared: yield WaitForAnimationDone then set state=2 ──────────────────
    waitAnim:
    Animator animForWait = @this.moongirl.dependencies.animator;
    <>2__current = CoroutineUtil.WaitForAnimationDone(
        animForWait, MoongirlAnims.ThrowMoonrangLoop);  // hash at MoongirlAnims static+0x14
    <>1__state = 2;
    return true;
}
```

> **Note on `goto` labels:** C# iterator state machines do not support `goto` across `yield return` boundaries
> at the source level.  The `countdown:` and `waitAnim:` labels in the reconstruction above represent the
> Ghidra `LAB_…` targets — in actual source code these would be structured as nested `if` / loop constructs.
> The `goto` form is used here purely to mirror the decompiled control flow.

---

## Notes on unknown / uncertain items

| Item | Observation |
|---|---|
| `FUN_1800127d0(animator, ...)` | Returns some object from which `float` at `result+0xe0 → +0xa0` is the animation clip duration. Possible match: `Animator.GetCurrentAnimatorStateInfo(0)` followed by a clip lookup. |
| `FUN_1800124d0(...)` | Called immediately before `UIManager.GetView<BattleScreen>()`. Consistent with `Manager<UIManager>.get_Instance()`. |
| `StringLiteral_1581` | The button name polled via `InputCategory.GetButton()`. Most likely `"Attack"` (confirm Moonrang throw) or `"Interact"`. Log analysis needed to confirm. |
| `WorldMapLevelName$$OnOutDone` | Ghidra resolves the IL2CPP symbol by the defining base/interface type. Semantically: plays the exit animation on the instruction text panel. |
| `LookDirectionController` sub-fields (`+0x18`, `+0x28`, `+0x2c`) | Not confirmed from dump.cs; described by access pattern only. |

---

## Patch implications

### Skipping instructions while keeping the throw

The only non-UI side-effects in state 0 are:
1. Orienting moongirl (`LookDirectionController.SetAngle`) — cosmetic, safe to keep or skip.
2. Playing `ThrowMoonrangIn` animation — needed for the throw to look correct during `WaitForAnimationDone`.
3. Setting `typingTimeLeft` — only relevant when instructions are shown; unused if we go directly to `waitAnim`.

**Minimal safe replacement** — skips the instruction UI and the input wait, but still plays the entry
animation and waits for it before throwing:

```csharp
[HarmonyPatch(typeof(MoonrangSpecialMove), "ShowInstructionsAndThrowMoonrangCoroutine")]
static class Patch_Moonrang_SkipInstructions
{
    static bool Prefix(MoonrangSpecialMove __instance, ref IEnumerator __result)
    {
        __result = SkipToThrow(__instance);
        return false;
    }

    static IEnumerator SkipToThrow(MoonrangSpecialMove instance)
    {
        // Keep the facing + animation setup so ThrowMoonrang fires from the right pose.
        Animator anim = instance.moongirl.dependencies.animator;
        anim.Play(MoongirlAnims.ThrowMoonrangIn, -1);
        yield return CoroutineUtil.WaitForAnimationDone(anim, MoongirlAnims.ThrowMoonrangLoop);
        instance.ThrowMoonrang();
    }
}

// Soonrang overrides slot 59 at its own RVA — must be patched separately.
[HarmonyPatch(typeof(Soonrang), "ShowInstructionsAndThrowMoonrangCoroutine")]
static class Patch_Soonrang_SkipInstructions
{
    static bool Prefix(Soonrang __instance, ref IEnumerator __result)
    {
        __result = SkipToThrow(__instance);
        return false;
    }

    static IEnumerator SkipToThrow(Soonrang instance)
    {
        Animator anim = instance.moongirl.dependencies.animator;
        anim.Play(MoongirlAnims.ThrowMoonrangIn, -1);
        yield return CoroutineUtil.WaitForAnimationDone(anim, MoongirlAnims.ThrowMoonrangLoop);
        instance.ThrowMoonrang();
    }
}
```

**Simplest possible replacement** — if animation timing turns out not to matter,
just `ThrowMoonrang()` immediately:

```csharp
static IEnumerator SkipToThrow(MoonrangSpecialMove instance)
{
    instance.ThrowMoonrang();
    yield break;
}
```

The Soonrang `d__41` state machine contains only `<>4__this` (no `typingTimeLeft` or `battleScreen`
fields), strongly suggesting its override already skips the typing UI and goes directly to
Soonrang-specific setup before calling base/ThrowMoonrang. Ghidra analysis of
`Soonrang.<>d__41$$MoveNext` (VA `0x180590D80`) is recommended before patching Soonrang.
