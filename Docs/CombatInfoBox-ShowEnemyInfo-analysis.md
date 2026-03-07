# `CombatInfoBox$$ShowEnemyInfo` — Decompiled Code Analysis

## Signature

```csharp
public void ShowEnemyInfo(EnemyCombatTarget enemyTarget)
// param_1 = this       (CombatInfoBox*)
// param_2 = enemyTarget
```

**This is the real entry point for enemy HP/weakness display during combat.**
`EnemyDescriptionPanel.SetEnemyTarget` is only used by the secondary target-inspect panel
and is never called during normal combat hover.

---

## Field / offset map

### CombatInfoBox (`param_1`, `longlong *******`, each index = 8 bytes)

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `param_1[3]` | `0x18` | `TextLocalizer textLocalizer` |
| `param_1[3][8]` | `0x18→+8` = `textLocalizer.textTyper` | `TextTyper textTyper` |
| `param_1[0xd]` | `0x68` | `GameObject resistancesSectionPrefab` |
| `param_1[0xe]` | `0x70` | `GameObject weaknessesSectionPrefab` |
| `param_1[0xf]` | `0x78` | `GameObject lifeBarSectionPrefab` |
| `param_1[0x11]` + `0x8c` | `0x88` / `0x8C` | `Color defaultTextColor` (R, G components) |
| `param_1[0x12]` + `0x94` | `0x90` / `0x94` | `Color defaultTextColor` (B, A components) |

### CombatTarget (`param_2`, base of EnemyCombatTarget)

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `param_2[0x10]` | `0x80` | `CombatActor owner` |
| `(*param_2[0x10])[0x39]` | vtable slot 57 | `owner.GetCurrentCharacterData()` (virtual) |
| `(*param_2[0x10])[0x3a]` | vtable slot 58 | related CharacterData method |
| `*(char *)(charData + 0xc)` | charData `+0x60` | bool flag — "show in box" / visibility |

### EnemyCombatActor (result of `EnemyCombatTarget$$GetOwner`)

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `plVar6[0x20]` (`longlong *`, index = 8 bytes) | `0x100` | `bool hideHP` |

---

## Original decompiled code (annotated)

```c
void CombatInfoBox$$ShowEnemyInfo(longlong *******param_1, longlong *******param_2,
                                   longlong ******param_3, longlong *******param_4)
{
  // ── Static init (one-time method-info pointer caching) ──────────────────
  if (DAT_183a12949 == '\0') {
    FUN_1802a4cc0(&Method$CharacterStatsManager.HasModifierOfType<ShowEnemyHPModifier>());
    FUN_1802a4cc0(&Method$CharacterStatsManager.HasModifierOfType<ShowEnemyWeaknessesModifier>());
    FUN_1802a4cc0(&CombatInfoBoxLifeBarSection_TypeInfo);
    FUN_1802a4cc0(&CombatInfoBoxResistancesSection_TypeInfo);
    FUN_1802a4cc0(&CombatInfoBoxWeaknessesSection_TypeInfo);
    FUN_1802a4cc0(&EnemyCharacterData_TypeInfo);
    FUN_1802a4cc0(&Method$Manager<CombatManager>.get_Instance());
    FUN_1802a4cc0(&Method$Manager<CharacterStatsManager>.get_Instance());
    DAT_183a12949 = '\x01';
  }

  // ── Clear all existing sections ────────────────────────────────────────
  ppppppplVar11 = null;
  CombatInfoBox$$ClearSections(param_1, ...);

  // ── Localize and display enemy name ────────────────────────────────────
  ppppppplVar10 = param_1[3];           // textLocalizer (offset 0x18)
  if (ppppppplVar10 == null) goto NullReturn;
  TextLocalizer$$ClearController(ppppppplVar10);
  pppppplVar12 = param_1[3];            // textLocalizer again
  if (param_2 == null) goto NullReturn;
  puVar4 = CombatTarget$$GetNameLocId(local_18, param_2, ...);   // → LocalizationId
  if (pppppplVar12 == null) goto NullReturn;
  local_38 = *puVar4;  uStack_30 = puVar4[1];   // copy LocalizationId struct
  TextLocalizer$$LocalizeText(pppppplVar12, &local_38);          // display name

  // ── Determine background color from CharacterData ──────────────────────
  // Read owner.GetCurrentCharacterData() via vtable [0x39]
  ppppppplVar10 = param_2[0x10];        // enemyTarget.owner (CombatActor @ 0x80)
  if (ppppppplVar10 == null) goto NullReturn;
  ppppppplVar11 = (*ppppppplVar10)[0x3a];   // vtable slot 0x3a → color field ptr
  param_3 = (*(code *)(*ppppppplVar10)[0x39])();  // vtable slot 0x39 → GetCurrentCharacterData()
  ppppppplVar14 = null;

  if (param_3 == null) goto UseDefaultColor;
  else {
    // Type-check: is param_3 an EnemyCharacterData?
    if ( /* IL2CPP type hierarchy check fails */ ||
         *(char *)(param_3 + 0xc) == '\0') {    // +0x60 bytes — "customColor" / "hasCustomColor" bool
      goto UseDefaultColor;
    }
    // param_3 is valid EnemyCharacterData with custom color
    if (param_1[3] == null) goto NullReturn;
    ppppplVar2 = param_1[3][8];                 // textLocalizer.textTyper (TextTyper)
    lVar5 = FUN_1800127d0(...);                 // some layout/size helper
    if (lVar5 == 0 || ppppplVar2 == null) goto NullReturn;
    param_4 = *ppppplVar2;
    // Copy custom color from EnemyCharacterData (at offsets 0xa8..0xb4)
    local_28 = *(lVar5 + 0xa8);    // Color.r
    uStack_24 = *(lVar5 + 0xac);   // Color.g
    uStack_20 = *(lVar5 + 0xb0);   // Color.b
    uStack_1c = *(lVar5 + 0xb4);   // Color.a
    param_3 = param_4[0x56];
    (*(code *)param_4[0x55])(ppppplVar2);  // set color on TextTyper
    goto ColorDone;
  }

UseDefaultColor:
  // Use defaultTextColor (offset 0x88 / 0x8C / 0x90 / 0x94 on CombatInfoBox)
  if (param_1[3] == null || (ppppppplVar10 = param_1[3][8]) == null) goto NullReturn;
  local_28 = *(param_1 + 0x11);          // defaultTextColor.r  (0x88)
  uStack_24 = *(param_1 + 0x8c);         // defaultTextColor.g
  uStack_20 = *(param_1 + 0x12);         // defaultTextColor.b  (0x90)
  uStack_1c = *(param_1 + 0x94);         // defaultTextColor.a
  param_3 = (*ppppppplVar10)[0x56];
  (*(code *)(*ppppppplVar10)[0x55])();    // set color on TextTyper

ColorDone:
  // ── HP section ────────────────────────────────────────────────────────
  plVar6 = EnemyCombatTarget$$GetOwner(param_2);   // → EnemyCombatActor
  if (plVar6 != null) {
    lVar5 = plVar6[0x20];      // owner.hideHP (EnemyCombatActor @ 0x100), as longlong (1 = true)

    ppppppplVar7 = Manager<CharacterStatsManager>.Instance;
    if (ppppppplVar7 != null) {
      // *** HasModifierOfType<ShowEnemyHPModifier>(includeGlobal: true) ***
      // CONCAT71(..., 1) packs includeGlobal=true into the bool parameter
      uVar8 = CharacterStatsManager$$HasModifierOfType<object>(
                  ppppppplVar7,
                  CONCAT71(..., 1),      // includeGlobal = true
                  Method$HasModifierOfType<ShowEnemyHPModifier>(),
                  param_4);

      if ((char)uVar8 != '\0' && (char)lVar5 == '\0') {
        // Modifier found AND owner.hideHP == false → show HP

        // AddSection(lifeBarSectionPrefab)         [param_1[0xf] → offset 0x78]
        ppppppplVar7 = CombatInfoBox$$AddSection(param_1, param_1[0xf], null, ...);
        if (ppppppplVar7 == null) goto NullReturn;

        // Type-cast result to CombatInfoBoxLifeBarSection (IL2CPP hierarchy check)
        if ( /* type check passes */ ) {
          // CombatInfoBoxLifeBarSection.Init(enemyTarget)
          // NOTE: the call reads as CombatInfoBoxTeamMatesSection$$Init — this is a
          // Ghidra mis-label; the actual dispatch is via vtable and resolves to
          // CombatInfoBoxLifeBarSection$$Init at runtime.
          CombatInfoBoxTeamMatesSection$$Init(ppppppplVar10, param_2);
        }
      }

      // ── Weaknesses / Resistances sections ────────────────────────
      plVar6 = Manager<CharacterStatsManager>.Instance;
      if (plVar6 != null) {
        // *** HasModifierOfType<ShowEnemyWeaknessesModifier>(includeGlobal: true) ***
        uVar8 = CharacterStatsManager$$HasModifierOfType<object>(
                    plVar6,
                    CONCAT71(..., 1),     // includeGlobal = true
                    Method$HasModifierOfType<ShowEnemyWeaknessesModifier>(),
                    param_4);

        if ((char)uVar8 != '\0') {
          // Modifier found → conditionally add resistances section
          uVar8 = CombatTarget$$HasResistance(param_2, ...);
          if ((char)uVar8 != '\0') {
            // AddSection(resistancesSectionPrefab)   [param_1[0xd] → offset 0x68]
            ppppppplVar7 = CombatInfoBox$$AddSection(param_1, param_1[0xd], null, ...);
            // Type-cast to CombatInfoBoxResistancesSection + GC write barrier
            ppppppplVar11 + 7 = param_2;   // section.target = enemyTarget
            (*(code *)(*ppppppplVar11)[0x33])();   // UpdateInfo()
          }

          // Conditionally add weaknesses section
          uVar8 = CombatTarget$$HasWeakness(param_2, ...);
          if ((char)uVar8 != '\0') {
            // AddSection(weaknessesSectionPrefab)    [param_1[0xe] → offset 0x70]
            ppppppplVar7 = CombatInfoBox$$AddSection(param_1, param_1[0xe], null, ...);
            // Type-cast to CombatInfoBoxWeaknessesSection + GC write barrier
            ppppppplVar14 + 7 = param_2;   // section.target = enemyTarget
            (*(code *)(*ppppppplVar14)[0x33])(ppppppplVar14);   // UpdateInfo()
            goto UpdateTextSize;
          }
        }
UpdateTextSize:
        CombatInfoBox$$UpdateTextSize(param_1, ...);
        return;
      }
    }
  }

NullReturn:
  FUN_1802845b0(...);   // IL2CPP NRE helper
  swi(3); return;
}
```

---

## Reconstructed C#

```csharp
public void ShowEnemyInfo(EnemyCombatTarget enemyTarget)
{
    ClearSections();

    // Display enemy name
    textLocalizer.ClearController();
    if (enemyTarget == null) return;
    textLocalizer.LocalizeText(CombatTarget.GetNameLocId(enemyTarget));

    // Set text color from CharacterData (or defaultTextColor if none)
    var charData = enemyTarget.owner.GetCurrentCharacterData() as EnemyCharacterData;
    Color color = (charData != null && charData.<hasCustomColor>)
                ? charData.<customColor>    // offsets 0xa8–0xb4 on EnemyCharacterData
                : defaultTextColor;         // offset 0x88 on CombatInfoBox
    textLocalizer.textTyper.<SetColor>(color);

    // ━━ HP section ━━
    EnemyCombatActor owner = enemyTarget.GetOwner();
    if (owner == null) return;

    // *** The ShowEnemyHPModifier check — this is what the mod must bypass ***
    bool showHP = Manager<CharacterStatsManager>.Instance
                      .HasModifierOfType<ShowEnemyHPModifier>(includeGlobal: true);

    if (showHP && !owner.hideHP)   // hideHP @ EnemyCombatActor+0x100
    {
        var section = (CombatInfoBoxLifeBarSection)AddSection(lifeBarSectionPrefab);
        section.Init(enemyTarget);   // sets section.target, populates lifeBar + hpTextfield
    }

    // ━━ Weaknesses / Resistances sections ━━
    bool showWeaknesses = Manager<CharacterStatsManager>.Instance
                              .HasModifierOfType<ShowEnemyWeaknessesModifier>(includeGlobal: true);

    if (showWeaknesses)
    {
        if (enemyTarget.HasResistance())
        {
            var s = (CombatInfoBoxResistancesSection)AddSection(resistancesSectionPrefab);
            s.target = enemyTarget;
            s.UpdateInfo();
        }
        if (enemyTarget.HasWeakness())
        {
            var s = (CombatInfoBoxWeaknessesSection)AddSection(weaknessesSectionPrefab);
            s.target = enemyTarget;
            s.UpdateInfo();
        }
    }

    UpdateTextSize();
}
```

---

## What this means for the mod

### Why the original patch did nothing
`EnemyDescriptionPanel.SetEnemyTarget` is never called during normal combat.
`CombatInfoBox.ShowEnemyInfo` is the real target — confirmed by log probe.

### Fix strategy
Postfix on `CombatInfoBox.ShowEnemyInfo`:
- `lifeBarSectionPrefab` is `public` → accessible directly as `__instance.lifeBarSectionPrefab`
- `AddSection` is `private` → needs `AccessTools.Method`
- `CombatInfoBoxLifeBarSection.Init(CombatTarget)` is `public`
- No need to check `hideHP` — we want to always show

```csharp
[HarmonyPatch(typeof(CombatInfoBox), nameof(CombatInfoBox.ShowEnemyInfo))]
static class Patch_CombatInfoBox_ShowEnemyInfo
{
    static readonly MethodInfo _addSection =
        AccessTools.Method(typeof(CombatInfoBox), "AddSection",
            new[] { typeof(GameObject) });   // check exact signature

    static void Postfix(CombatInfoBox __instance, EnemyCombatTarget enemyTarget)
    {
        // If the life bar section was already added (modifier present), nothing to do.
        // Otherwise force-add it.
        // AddSection → Init(enemyTarget)
    }
}
```

### The weaknesses mod
Same fix needed — Postfix on `CombatInfoBox.ShowEnemyInfo` always adds the
resistances/weaknesses sections regardless of `HasModifierOfType<ShowEnemyWeaknessesModifier>`.
`HasResistance` / `HasWeakness` checks can remain (no point showing empty sections).
