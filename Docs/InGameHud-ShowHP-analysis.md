# `InGameHud$$ShowHP` — Decompiled Code Analysis

## Signature

```csharp
public void ShowHP(AnimatorUpdateMode animatorUpdateMode = 0)
// param_1 = this (InGameHud*)
// param_2 = animatorUpdateMode — masked to 32-bit: ppppplVar7 = (ulonglong)param_2 & 0xffffffff
```

---

## TL;DR — This function is irrelevant to the ShowEnemyHP mod

`InGameHud.ShowHP` shows the **player party HP/AP bar panel** in the bottom HUD.
It calls `PlayerStatusPanel.AddPlayerStatus` for each combat party member.
It has **nothing to do with enemy HP visibility**.

---

## Field / offset map

### InGameHud (`param_1`)

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `param_1[0x10]` | `0x80` | `PlayerStatusPanel playerStatuses` |

### PlayerStatusPanel (base: InGameHudPanel → MonoBehaviour)

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `*(char *)(lVar5 + 0x20)` | `0x20` | `bool opened` *(from InGameHudPanel base)* |
| `*(longlong *)(lVar5 + 0x38)` | `0x38` | `List<PlayerStatusUI> playerStatusList` |
| `*(undefined4 *)(lVar5 + 0x48)` | `0x48` | `int statusCount` |

---

## Original decompiled code

```c
void InGameHud$$ShowHP(longlong *param_1, longlong *****param_2,
                       ulonglong *param_3, longlong ******param_4)
{
  longlong *plVar1;
  code *pcVar2;
  longlong lVar3;
  longlong *plVar4;
  longlong lVar5;
  longlong *plVar6;
  longlong *****ppppplVar7;
  uint uVar8;
  uint uVar9;

  // Extract AnimatorUpdateMode enum value (32-bit)
  ppppplVar7 = (longlong *****)((ulonglong)param_2 & 0xffffffff);
  plVar6 = param_1;

  // ── Static init ────────────────────────────────────────────────────
  // One-time initialization of method info pointers:
  //   List<CharacterDefinitionId>.get_Count / get_Item
  //   Manager<PlayerPartyManager>.get_Instance
  //   Manager<CharacterStatsManager>.get_Instance
  if (DAT_183a12e3f == '\0') {
    FUN_1802a4cc0(&Method$List<CharacterDefinitionId>.get_Count());
    FUN_1802a4cc0(&Method$List<CharacterDefinitionId>.get_Item());
    FUN_1802a4cc0(&Method$Manager<PlayerPartyManager>.get_Instance());
    plVar6 = &Method$Manager<CharacterStatsManager>.get_Instance();
    FUN_1802a4cc0(&Method$Manager<CharacterStatsManager>.get_Instance());
    DAT_183a12e3f = '\x01';
  }

  // ── CharacterStatsManager.CapHPAndMP ───────────────────────────────
  // Clamps all party HP/MP to their max values before displaying.
  plVar1 = FUN_180014930(plVar6, ...);   // Manager<CharacterStatsManager>.get_Instance()
  if (plVar1 != null) {
    CharacterStatsManager$$CapHPAndMP(plVar1, 0, ...);

    // ── Guard: skip if playerStatuses panel is already open ──────────
    lVar5 = param_1[0x10];              // this.playerStatuses (offset 0x80)
    plVar6 = plVar1;
    if (lVar5 != 0) {
      if (*(char *)(lVar5 + 0x20) != '\0') {
        return;                         // playerStatuses.opened == true → already shown, bail
      }

      // ── Clear existing PlayerStatusUI items ──────────────────────
      // Loops through playerStatusList (offset 0x38), calls:
      //   go = playerStatusList[i].gameObject
      //   go.SetActive(false)
      // Uses lazy-cached UnityEngine.Component::get_gameObject() and
      // UnityEngine.GameObject::SetActive() native method ptrs.
      // uVar9 = loop index; plVar6 = current PlayerStatusUI
      if (DAT_183a12e9c == '\0') {
        FUN_1802a4cc0(&Method$List<PlayerStatusUI>.get_Count());
        FUN_1802a4cc0(&Method$List<PlayerStatusUI>.get_Item());
        DAT_183a12e9c = '\x01';
      }
      uVar8 = 0;
      uVar9 = 0;
      plVar1 = (longlong *)0x0;
      lVar3 = *(longlong *)(lVar5 + 0x38);   // playerStatusList (List object)
      while (plVar6 = plVar1, lVar3 != 0) {
        if (*(int *)(lVar3 + 0x18) <= (int)plVar1) {
          *(undefined4 *)(lVar5 + 0x48) = 0;  // statusCount = 0
          goto LAB_180df75d0;                  // proceed to AddPlayerStatus phase
        }
        // get playerStatusList[uVar9]
        lVar3 = *(longlong *)(lVar5 + 0x38);
        if (lVar3 == 0) break;
        if (*(uint *)(lVar3 + 0x18) <= uVar9) goto ThrowArgumentOutOfRange;
        plVar6 = *(longlong **)(lVar3 + 0x10);   // List._items backing array
        if (plVar6 == null) break;
        if (*(uint *)(plVar6 + 3) <= uVar9) goto ThrowArgumentOutOfRange;
        plVar1 = (longlong *)plVar6[(longlong)(int)uVar9 + 4];  // playerStatusList[uVar9]
        if (plVar1 == null) break;

        // Lazy-cache UnityEngine.Component::get_gameObject
        pcVar2 = DAT_183a1fbb8;
        if (pcVar2 == null) { pcVar2 = FUN_18029a8a0("UnityEngine.Component::get_gameObject()"); }
        DAT_183a1fbb8 = pcVar2;
        lVar3 = (*pcVar2)();                         // playerStatusUI.gameObject
        plVar6 = plVar1;
        if (lVar3 == 0) break;

        // Lazy-cache UnityEngine.GameObject::SetActive
        pcVar2 = DAT_183a1fc78;
        if (pcVar2 == null) { pcVar2 = FUN_18029a8a0("UnityEngine.GameObject::SetActive(Boolean)"); }
        DAT_183a1fc78 = pcVar2;
        (*pcVar2)(lVar3);             // gameObject.SetActive(false)   [param_2 = 0 = false]

        uVar9 = uVar9 + 1;
        plVar1 = (longlong *)(ulonglong)uVar9;
        lVar3 = *(longlong *)(lVar5 + 0x38);
      }
    }
  }

LAB_180df776a:    // fallback / null-guard exit → InGameHudPanel base Show()
  FUN_1802845b0(plVar6, ...);   // InGameHudPanel$$Show() or similar base call
  return;

  // ── Add player statuses phase ─────────────────────────────────────
LAB_180df75d0:
  // Navigate: PlayerPartyManager.Instance → currentPartyCharacters list
  // For each character in the combat party:
  //   call PlayerStatusPanel$$AddPlayerStatus(plVar1/*playerStatuses*/, charId, null)
  //   then loop via goto LAB_180df75d0

  plVar4 = PlayerPartyManager.Instance;          // via Method+0x20 static ptr
  // (IL2CPP type-check cast of plVar4)
  plVar1 = *(longlong **)plVar4[0x18];           // field at 0xC0 on PlayerPartyManager
                                                 // = List<PartyCharacterFollower> followers
                                                 // (or neighbouring list; ptr chain navigates
                                                 //  to the runtime combat party ID list)
  // (IL2CPP type-check cast of plVar1)

  // Deep pointer chain: plVar1[0x17] → 0xB8 → some list handle → +0x98 → List backing
  // Effectively: get current combat party CharacterDefinitionId list
  if (plVar1[0x17] == 0 ||
      (lVar5 = *(plVar1[0x17] + 0x10 + 0x98)) == 0 ||
      (plVar6 = *(lVar5 + 0x38)) == null) goto LAB_180df776a;

  plVar1 = param_1[0x10];                        // playerStatuses panel

  if ((int)plVar6[3] <= (int)uVar8) {
    // List exhausted — set animator.updateMode to animatorUpdateMode param
    // then call animator.Play("InState")/equivalent via vtable 0x178/0x180
    if (plVar1 != null && plVar1[3] != 0) {
      (*DAT_183a1c8b8)(plVar1[3]);               // Animator::set_updateMode(animatorUpdateMode)
      plVar6 = plVar1[0x10];                     // playerStatuses.animator
      if (plVar6 != null) {
        (**(plVar6->vtable + 0x178))(plVar6, *(plVar6->vtable + 0x180));  // Play anim
        return;
      }
    }
    goto LAB_180df776a;
  }

  // Get charId at index uVar8 from combat party list
  plVar4 = PlayerPartyManager.Instance;  // (again, same pattern)
  // ... navigate to same combat party list ...
  if (list.Count <= uVar8)  ThrowArgumentOutOfRange();
  param_4 = list._items[uVar8];          // CharacterDefinitionId at index uVar8
  param_3 = null;
  // Add this character's status bar to the panel
  PlayerStatusPanel$$AddPlayerStatus(plVar1, param_4, null, ...);
  uVar8 = uVar8 + 1;
  goto LAB_180df75d0;                    // loop until all party members added
}
```

---

## Reconstructed C#

```csharp
public void ShowHP(AnimatorUpdateMode animatorUpdateMode = AnimatorUpdateMode.Normal)
{
    // Clamp all party HP/MP to max before display
    Manager<CharacterStatsManager>.Instance.CapHPAndMP();

    PlayerStatusPanel panel = playerStatuses;   // offset 0x80

    if (panel != null)
    {
        // Idempotent guard — don't rebuild if already visible
        if (panel.opened)   // InGameHudPanel.opened @ 0x20
            return;

        // Clear any existing PlayerStatusUI elements
        foreach (var ui in panel.playerStatusList)   // @ 0x38
            ui.gameObject.SetActive(false);
        panel.statusCount = 0;    // @ 0x48

        // Add one status bar per combat party member
        var partyIds = Manager<PlayerPartyManager>.Instance
                           .<navigated combat party CharacterDefinitionId list>;
        foreach (var characterId in partyIds)
            panel.AddPlayerStatus(characterId);

        // Trigger the "in" animator state with the requested update mode
        panel.animator.updateMode = animatorUpdateMode;
        panel.animator.Play(IN_STATE);
        return;
    }

    base.Show();    // InGameHudPanel fallback
}
```

---

## Why this is irrelevant to ShowEnemyHP

| | `InGameHud.ShowHP` | ShowEnemyHP goal |
|---|---|---|
| **What it shows** | Player party HP/AP bars (bottom HUD strip) | Enemy HP number on the enemy panel |
| **Key call** | `PlayerStatusPanel.AddPlayerStatus(charId)` | HP text on `EnemyDescriptionPanel` or `CombatInfoBox` |
| **`ShowEnemyHPModifier`** | Never referenced | Controls the inlined modifier check in `SetEnemyTarget`/`ShowEnemyInfo` |
| **Patchable for our goal** | No | `CombatInfoBox.ShowEnemyInfo` is the correct target |

The `ShowEnemyHPModifier` and `EnemyCombatActor.hideHP` gating is entirely inside
`CombatInfoBox.ShowEnemyInfo` and/or `EnemyDescriptionPanel.SetEnemyTarget`.
`InGameHud.ShowHP` never consults either.
