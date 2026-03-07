# `EnemyDescriptionPanel$$SetEnemyTarget` — Decompiled Code Analysis

## Signature

```csharp
public void SetEnemyTarget(EnemyCombatTarget enemyTarget)
// param_1 = this   (EnemyDescriptionPanel*)
// param_2 = enemyTarget
```

---

## Field offset map

`param_1` is typed as `undefined1 (*)[16]` — each index step is **16 bytes**.  
`param_1[N]` = byte offset `N × 16`; `param_1[N] + 8` = the second 8-byte slot in that chunk.

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `param_1[3]` (first 8 bytes) | `0x30` | `TextMeshProUGUI enemyNameField` |
| `param_1[3] + 8` | `0x38` | `TextMeshProUGUI enemyHPField` |
| `param_1[4]` (first 8 bytes) | `0x40` | `LifeBar lifeBar` |
| `param_1[4] + 8` | `0x48` | `CanvasGroup lifeBarCanvasGroup` |
| `*(float*)(param_1[5] + 8)` | `0x58` | `float attributeHeight` |
| `*(float*)(param_1[5] + 0xc)` | `0x5C` | `float attributeSpacing` |
| `*(float*)param_1[6]` | `0x60` | `float attributeTopPadding` |
| `param_1[7]` (first 8 bytes) | `0x70` | `RectTransform strengthContainer` |
| `param_1[7] + 8` (via `param_1[10]+8` in context) → actually | `0xA8` | `List<GameObject> weaknesses` |
| `param_1[0xb]` | `0xB0` | `List<GameObject> strengths` |
| `param_1[0xb] + 8` | `0xB8` | `int spaceUnderEnemyName` |

`plVar9` = `EnemyCombatTarget.GetOwner()` → `EnemyCombatActor`:

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `(char)plVar9[0x20]` (longlong* index) | `0x100` | `bool hideHP` |

---

## Reconstructed C#

```csharp
public void SetEnemyTarget(EnemyCombatTarget enemyTarget)
{
    // ── Name ──────────────────────────────────────────────────────────
    if (enemyTarget == null) goto NullReturn;

    var nameLocId = CombatTarget.GetNameLocId(enemyTarget);
    var locMgr    = Manager<LocalizationManager>.Instance;
    if (locMgr == null) goto NullReturn;

    string enemyName = locMgr.GetText(nameLocId);

    if (enemyNameField == null) goto NullReturn;
    enemyNameField.SetText(enemyName);

    // ── HP bar fill ───────────────────────────────────────────────────
    // Virtual calls on EnemyCombatTarget for current/max HP
    int    currentHP = enemyTarget.GetCurrentHP();      // vtable 0x1d8 on EnemyCombatTarget
    int    maxHP     = enemyTarget.GetMaxHP();           // vtable 0x1c8/0x1d0 on EnemyCombatTarget

    PositionStuff();

    if (lifeBar != null)
    {
        float newFill = (float)currentHP / (float)maxHP;

        // Only update if the fill value actually changed (avoids redundant redraws)
        if (!Mathf.Approximately(lifeBar.someCurrentFillValue, newFill))  // lifeBar+0x60
        {
            lifeBar.someHideObject.SetActive(false);   // lifeBar+0x50, vtable 0x178/0x180
            lifeBar.someOtherObject.SetActive(false);  // lifeBar+0x58, vtable 0x178/0x180
            lifeBar.someCurrentFillValue = newFill;
        }
    }

    // ── HP text & visibility ──────────────────────────────────────────
    // *** This check is INLINED — MustShowHP() is never called ***
    bool showHP = Manager<CharacterStatsManager>.Instance
                      .HasModifierOfType<ShowEnemyHPModifier>(includeGlobal: true);

    string hpText;
    if (!showHP)
    {
        // Hide the HP bar canvas group
        lifeBarCanvasGroup.alpha = 0f;
        hpText = "";   // StringLiteral_7624
    }
    else
    {
        // Raw decompiled (Ghidra), annotated:
        //
        //   pauVar14 = 0;                      // local span buffer = null (FormatInt32 output)
        //   pauVar13 = param_2;                // save enemyTarget ref
        //
        //   // owner = enemyTarget.GetOwner()
        //   plVar9 = EnemyCombatTarget$$GetOwner(param_2);
        //   if (plVar9 == null) goto NullReturn;
        //
        //   // if (owner.hideHP != 0)  →  plVar9 is longlong*, [0x20]*8 = offset 0x100
        //   if ((char)plVar9[0x20] != '\0') goto LAB_180d47993;   // HideHPPath
        //
        //   // ShowHP() — ICF-folded to same RVA as OnExitPool
        //   EnemyDescriptionPanel$$OnExitPool(param_1, 0, param_3, param_4);
        //
        //   pauVar7 = *(param_1[3] + 8);       // enemyHPField (offset 0x38)
        //   uVar8  = *(vtable(param_2)+0x1e0); // method ptr — used only for IL2CPP
        //                                      //   Number type-dispatch, NOT the HP value
        //   uVar4  = (*vtable(param_2)[0x1d8])(param_2);  // GetCurrentHP() → int
        //   pauVar13 = uVar4;                  // store currentHP
        //
        //   // Static init for System.Number (once per process)
        //   if (DAT_183a1775d == '\0') {
        //       FUN_1802a4cc0(&System.Number_TypeInfo);
        //       FUN_1802a4cc0(&Method$ReadOnlySpan<char>.op_Implicit());
        //       DAT_183a1775d = '\x01';
        //   }
        //   // Thread-safe guard for op_Implicit
        //   if ((*(op_Implicit_method+0x20)[0x132] & 1) == 0)
        //       FUN_1802d0c80(op_Implicit_method+0x20, uVar8, ...);
        //   // TypeInfo fully-initialized guard
        //   if ((int)Number_TypeInfo[0x1c] == 0)
        //       FUN_180310620(Number_TypeInfo, uVar8, ...);
        //
        //   // currentHP.ToString() via System.Number.FormatInt32
        //   local_38 = zeroed 16-byte span;
        //   pauVar14 = &local_38;
        //   uVar10 = System.Number$$FormatInt32(uVar4, pauVar14, null, null); // → string*
        //   // falls through to: TextMeshProUGUI$$SetText(pauVar7, uVar10, ...)

        EnemyCombatActor owner = enemyTarget.GetOwner();
        if (owner == null) goto NullReturn;

        if (owner.hideHP)             // EnemyCombatActor+0x100 — per-enemy designer flag
        {
            goto HideHPPath;          // same as !showHP path
        }

        OnExitPool();                 // resets / shows the full panel (== ShowHP())
        hpText = currentHP.ToString();  // System.Number.FormatInt32 → string
    }

    // ── HP field text + weaknesses/strengths ─────────────────────────
    if (enemyHPField != null)
    {
        enemyHPField.SetText(hpText);

        SetWeaknesses(enemyTarget);   // *** MustShowWeaknesses() called inside here ***
        SetStrengths(enemyTarget);

        // ── Layout: resize weakness and strength containers ───────────
        // numWeaknessRows = weaknesses.Count / 5 + 1
        // numStrengthRows = strengths.Count  / 5 + 1
        //
        // containerHeight = (numRows - 1) * attributeSpacing
        //                 + numRows * attributeHeight
        //                 + attributeTopPadding
        //
        // Applied via RectTransform.sizeDelta on weaknessContainer and strengthContainer.

        if (weaknessContainer != null)
        {
            int numRows = weaknesses.Count / 5 + 1;
            var delta = weaknessContainer.sizeDelta;
            delta.y = (numRows - 1) * attributeSpacing
                     + numRows * attributeHeight
                     + attributeTopPadding;
            weaknessContainer.sizeDelta = delta;
        }

        if (strengthContainer != null)
        {
            // same formula for strengths
            int numRows = strengths.Count / 5 + 1;
            var delta = strengthContainer.sizeDelta;
            delta.y = (numRows - 1) * attributeSpacing
                     + numRows * attributeHeight
                     + attributeTopPadding;
            strengthContainer.sizeDelta = delta;
        }
    }
    return;

NullReturn:
    // IL2CPP NRE helper — unreachable in normal play
    throw new NullReferenceException();
}
```

---

## Why ShowEnemyHP and ShowEnemyWeaknesses mods don't work

### The inlining problem

Both mods patch the private helper methods:

```csharp
[HarmonyPatch(typeof(EnemyDescriptionPanel), "MustShowHP")]
// and
[HarmonyPatch(typeof(EnemyDescriptionPanel), "MustShowWeaknesses")]
```

**IL2CPP inlines both of these into their call site.**  
`SetEnemyTarget` never executes a call instruction to `MustShowHP()` — instead the IL2CPP compiler emits the `HasModifierOfType` check directly inline at the usage point.  The standalone `MustShowHP` function at RVA `0xD48530` exists in the binary (for possible reflection/vtable use) but `SetEnemyTarget` never branches to it.

Patching a method that is never called has no effect.

### `MustShowWeaknesses` is different

`SetWeaknesses` and `SetStrengths` are always called (they run regardless of the HP modifier result). `MustShowWeaknesses()` appears to be called from within `SetWeaknesses` itself, not inlined into `SetEnemyTarget`. So its patch *may* work — but only if `SetWeaknesses` is actually reached, which depends on `enemyHPField != null`.

### The `hideHP` flag

Even if the `HasModifierOfType` check were bypassed, `EnemyCombatActor.hideHP` (at offset `0x100`) is checked second:

```csharp
if (owner.hideHP) goto HideHPPath;
```

Bosses or special enemies with `hideHP = true` will always hide HP regardless of the modifier. Any fix needs to handle both the modifier check and this per-actor flag.

---

## Correct fix: patch `SetEnemyTarget` directly

The cleanest approach is a **Prefix** on `SetEnemyTarget` that clears `hideHP` on the actor, combined with a patch on `CharacterStatsManager.HasModifierOfType` for the specific modifier type — or a Postfix on `SetEnemyTarget` that force-invokes `ShowHP()` / `OnExitPool()` and re-formats the HP text.

Alternatively, since `HideHP()` and `ShowHP()` are non-inlined methods with their own RVAs, patching `HideHP` to no-op prevents the bar from being hidden, though the numeric text would still be blank.
