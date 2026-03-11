# `PotionKick.WaitForSeraiReadyAndThrowPotionsCoroutine` — Analysis

`PotionKick.<WaitForSeraiReadyAndThrowPotionsCoroutine>d__50$$MoveNext`
RVA `0x53C7E0`

---

## Original C (Ghidra)

```c
undefined8
PotionKick.<>d__50$$MoveNext
          (longlong param_1,undefined8 param_2,ulonglong *param_3,longlong ******param_4)

{
  undefined8 *puVar1;
  uint uVar2;
  IMAGE_DOS_HEADER *pIVar3;
  longlong ***ppplVar4;
  longlong *****ppppplVar5;
  longlong ****pppplVar6;
  code *pcVar7;
  undefined8 uVar8;
  char cVar9;
  ulonglong uVar10;
  longlong lVar11;
  longlong ******pppppplVar12;
  longlong lVar13;
  undefined1 (*pauVar14) [16];
  uint *puVar15;
  ulonglong *puVar16;
  undefined8 uVar17;
  undefined1 (*pauVar18) [16];
  IMAGE_DOS_HEADER *pIVar19;
  float fVar20;
  float fVar21;
  undefined8 local_78;
  undefined8 uStack_70;
  
  if (DAT_183a0f765 == '\0') {
    FUN_1802a4cc0((longlong *)&System.Action_TypeInfo);
    FUN_1802a4cc0(&Method$Manager<UIManager>.get_Instance());
    FUN_1802a4cc0(&Method$Manager<CombatManager>.get_Instance());
    FUN_1802a4cc0((longlong *)&Method$PotionKick.OnKick());
    FUN_1802a4cc0((longlong *)&ReshanAnims_TypeInfo);
    FUN_1802a4cc0((longlong *)&SeraiAnims_TypeInfo);
    FUN_1802a4cc0((longlong *)&Method$UIManager.GetView<BattleScreen>());
    DAT_183a0f765 = '\x01';
  }
  uVar2 = *(uint *)(param_1 + 0x10);
  uVar10 = (ulonglong)(int)uVar2;
  pIVar3 = *(IMAGE_DOS_HEADER **)(param_1 + 0x20);
  if (6 < uVar2) {
switchD_18053c898_default:
    return uVar10 & 0xffffffffffffff00;
  }
  /* ... full body shown in original prompt ... */
}
```

---

## State Machine Layout

`param_1` is the compiler-generated iterator object `d__50`.  
`pIVar3` (`*(param_1 + 0x20)`) = the outer `PotionKick` instance (`<>4__this`).

Ghidra types `pIVar3` as `IMAGE_DOS_HEADER*` (size `0x80` in this context), so:

| Expression | Byte offset on `PotionKick` | C# field |
|---|---|---|
| `*(uint*)(param_1 + 0x10)` | state machine `+0x10` | `<>1__state` (int) |
| `*(param_1 + 0x18)` | state machine `+0x18` | `<>2__current` (object) |
| `*(param_1 + 0x20)` | state machine `+0x20` | `<>4__this` (PotionKick) |
| `*(param_1 + 0x28)` | state machine `+0x28` | `<battleScreen>5__2` (BattleScreen) |
| `*(float*)(param_1 + 0x30)` | state machine `+0x30` | `<typingTimeLeft>5__3` (float) |

| Expression | Byte offset on `PotionKick` | C# field |
|---|---|---|
| `*(longlong*)&pIVar3[2].e_cparhdr` | `+0x108` | `serai` (SeraiCombatActor*) |
| `*(longlong*)&pIVar3[2].e_sp` | `+0x110` | `seraiKickState` (KickPotionState*) |
| `*(longlong*)(pIVar3 + 2)` | `+0x100` | `reshan` (ReshanCombatActor*) |
| `(char)pIVar3[3].e_sp` | `+0x190` | `seraiInPosition` (bool) |
| `*(undefined1*)((longlong)&pIVar3[3].e_csum + 1)` | `+0x193` | `updateKicks` (bool) |
| `*(int*)(pIVar3[1].e_program + 0x38)` | `+0xF8` | `potionThrowDoneCount` (int) |
| `*(int*)&pIVar3[1].e_cparhdr` | `+0x88` | `potionAmountToThrow` (int) |
| `*(longlong*)(pIVar3[1].e_program + 0x10)` | `+0xD0` | `instructionsLocId` (first 8 bytes) |
| `*(longlong*)(pIVar3[1].e_program + 0x18)` | `+0xD8` | `instructionsLocId` (second 8 bytes) |

`KickPotionState.kickCallback` is at `seraiKickState + 0x58` (confirmed from dump).

### Animation indices (shared static fields)

| Code expression | Static-field byte offset | Field |
|---|---|---|
| `SeraiAnims_TypeInfo[0xb]+8, +5 (ptr arith)` | `0x28` | `SeraiAnims.KickIn` |
| `ReshanAnims_TypeInfo[0xb]+8, (longlong)+0x44 (byte)` | `0x44` | `ReshanAnims.PotionKickThrow1` |
| `ReshanAnims_TypeInfo[0xb]+8, +2 (ptr arith)` | `0x10` | `ReshanAnims.SplitPotion` |
| `ReshanAnims_TypeInfo[0xb]+8, +9 (ptr arith)` | `0x48` | `ReshanAnims.PotionKickThrow3` |

---

## State Transition Map

```
Initial call (state = -1)
  │
  ▼ default:
  ┌─ seraiInPosition == false? ──► yield null  ──► state = 1 ──► loops back to default
  │
  └─ seraiInPosition == true
       Play SeraiAnims.KickIn on serai.animator
       yield WaitForAnimationDone(serai.animator, KickIn)  ──► state = 2

state = 2:
  battleScreen = UIManager.GetView<BattleScreen>()
  battleScreen.ShowStatInfos(false)           // hide stat infos
  controller = serai.player.GetLastActiveController()
  TextTyper.TypeText(battleUI_typingTarget, instructionsLocId, serai.player, controller)
  typingTimeLeft = textTyper.totalDuration    // e.g. 2–3 s
  ──► falls through to state 3 body

state = 3 (per-frame):
  typingTimeLeft -= Time.deltaTime
  if typingTimeLeft > 0: yield null  ──► state = 3
  // text has finished typing; wait for player to dismiss
  if !PlayerInputs.GetInteract(): yield null  ──► state = 3
  // interact pressed → dismiss
  battleUI_dismissTarget.OnOutDone()          // hide instruction panel
  battleScreen = null
  ──► break → common code

──── common code (reached from break in state 2 and state 3) ────
  lVar11 = seraiKickState                     // PotionKick + 0x110
  Action kickAction = new Action(PotionKick.OnKick)   // ← KEY: wires the QTE callback
  seraiKickState.kickCallback = kickAction            // KickPotionState + 0x58
  StateMachine.SetState(serai.stateMachine, seraiKickState, immediate: true)
  // (two virtual calls — likely stateMachineStateChanging notification)
  Play ReshanAnims.SplitPotion on reshan.animator
  yield WaitForAnimationDone(reshan.animator, SplitPotion)  ──► state = 4

state = 4:
  updateKicks = true                          // PotionKick + 0x193
  reshan.animEventHandler.onAttackHit += kickCallback_reference
  Play ReshanAnims.PotionKickThrow1 on reshan.animator
  ──► falls through to state 5 body

state = 5 (per-frame):
  if potionThrowDoneCount < potionAmountToThrow - 1:
      yield null  ──► state = 5
  // all-but-last potions thrown
  animState = reshan.animator.GetCurrentAnimatorStateInfo()
  yield WaitForAnimationDone(reshan.animator, animState)  ──► state = 6

state = 6:
  Play ReshanAnims.PotionKickThrow3 on reshan.animator
  ──► return false (coroutine done)
```

---

## Full Decompiled Approximate C#

```csharp
// iterator state machine — reconstructed as if written as a single method
private IEnumerator WaitForSeraiReadyAndThrowPotionsCoroutine()
{
    // ── Phase 1: wait until Serai has reached her throw position ─────────────
    while (!seraiInPosition)
        yield return null;

    // ── Phase 2: play Serai's kick-wind-up animation ─────────────────────────
    if (serai != null)
    {
        Animator seraiAnim = serai.GetAnimator();  // serai.animator
        if (seraiAnim != null)
        {
            seraiAnim.Play(SeraiAnims.KickIn, -1);
            yield return CoroutineUtil.WaitForAnimationDone(seraiAnim, SeraiAnims.KickIn);
        }
    }

    // ── Phase 3: show instruction UI ─────────────────────────────────────────
    BattleScreen battleScreen = UIManager.Instance.GetView<BattleScreen>();
    battleScreen.ShowStatInfos(false);

    // Type the kick-instruction text on screen
    Player seraiPlayer       = serai.Player;
    Controller lastController = seraiPlayer?.GetLastActiveController();
    TextTyper instructionTyper = battleScreen.GetTypingTarget();
    instructionTyper.TypeText(instructionsLocId, seraiPlayer, lastController);

    // Store how long the text takes to appear
    float typingTimeLeft = instructionTyper.totalDuration;

    // ── Phase 4: wait for typing + player dismiss ─────────────────────────────
    while (typingTimeLeft > 0f)
    {
        typingTimeLeft -= Time.deltaTime;
        yield return null;
    }

    while (!PlayerInputs.GetInteract(serai.inputReceiver))
        yield return null;

    battleScreen.GetDismissTarget().OnOutDone();
    battleScreen = null;

    // ── Phase 5: wire kickCallback and activate kick state ───────────────────
    //
    // THIS IS THE KEY STEP: a new Action wrapping OnKick() is created here and
    // stored into KickPotionState.kickCallback (+0x58).  KickPotionState reads
    // this callback every time the player presses Attack during the kick window.
    //
    seraiKickState.kickCallback = new Action(OnKick);

    // Transition Serai's state machine into the kick-accepting state
    serai.StateMachine.SetState(seraiKickState, immediate: true);
    // (one or two virtual callbacks to notify state-machine listeners)

    // ── Phase 6: Reshan throw animations ─────────────────────────────────────
    Animator reshanAnim = reshan.GetAnimator();
    if (reshanAnim != null)
    {
        reshanAnim.Play(ReshanAnims.SplitPotion, -1);
        yield return CoroutineUtil.WaitForAnimationDone(reshanAnim, ReshanAnims.SplitPotion);
    }

    // ── Phase 7: enable kick processing, subscribe hit event ─────────────────
    updateKicks = true;  // PotionKick.Update() now calls UpdateKicks() every frame

    PlayerAnimationEventHandler reshanEvents = reshan.GetAnimationEventHandler();
    reshanEvents.onAttackHit += /* kickCallback reference */;

    // Reshan throw loop (first set of potions)
    reshanAnim?.Play(ReshanAnims.PotionKickThrow1, -1);

    // ── Phase 8: wait until all-but-last potion has been thrown ──────────────
    while (potionThrowDoneCount < potionAmountToThrow - 1)
        yield return null;

    // Wait for current animation to finish (Reshan mid-throw)
    if (reshanAnim != null)
    {
        AnimatorStateInfo info = reshanAnim.GetCurrentAnimatorStateInfo(0);
        yield return CoroutineUtil.WaitForAnimationDone(reshanAnim, info.shortNameHash);
    }

    // ── Phase 9: play final throw animation ──────────────────────────────────
    reshanAnim?.Play(ReshanAnims.PotionKickThrow3, -1);
}
```

---

## Notes for Modding

### Where `kickCallback` is set
The `Action` wrapping `PotionKick.OnKick()` is created and stored into
`seraiKickState.kickCallback` **once**, inside Phase 5 above, every time the move
executes.  It is **not** set during `Awake`, `Preload`, or `DoMove` — it is created
fresh each combo.

Patch implications:
- Patching `WaitForSeraiReadyAndThrowPotionsCoroutine` (this coroutine) allows
  intercepting before the kick state is even live.
- Replacing `seraiKickState.kickCallback` after Phase 5 would override the QTE
  target entirely.
- `updateKicks` (Phase 7, `PotionKick + 0x193`) gates all kick processing in
  `PotionKick.Update`.  Before it is set to `true`, no kick distance checks run.

### State numbers in MoveNext
| `<>1__state` value | Phase |
|---|---|
| `-1` or `0` (first entry) | initial / spin-wait for `seraiInPosition` |
| `1` | still waiting for `seraiInPosition` (loops through `default:`) |
| `2` | after Serai animation done; about to show instructions |
| `3` | waiting for text + interact |
| `4` | kick state live; animation in progress |
| `5` | waiting for `potionThrowDoneCount` |
| `6` | final Reshan animation playing |

### `updateKicks` flag
Set to `true` in state 4 (Phase 7).  Until this point, `PotionKick.Update →
UpdateKicks → UpdateKickForPlayer → HasAPlayerKickedPotion` is never called.
Any auto-kick patch using `StateExecute` prefix can therefore safely assume
potions are in-flight once state ≥ 4.
