# NewDialogBoxInstance.PlayDialog — Ghidra Analysis

Covers `NewDialogBoxInstance$$PlayDialog` (VA `0x180E26410`), the method that
initialises and renders one dialog-box widget — text, portrait, name-plate, and
continue arrow — for a single dialog event dispatched by a cutscene behaviour-tree
node.

**Relevance to SeraiDefaultSkin mod:** this is the call site where the cutscene
`customPortrait` sprite override can bypass `CharacterDefinitionManager` and supply
a stale ROBOT portrait sprite, and where `NewDialogBoxPortrait.SetPortrait` is
ultimately called.

---

## Class context

```
NewDialogBox : View, IDialogBox         // the manager-level dialog box
  ├── NewDialogBoxInstance dialogBoxLeft        // 0x88
  ├── NewDialogBoxInstance dialogBoxRight       // 0x90
  ├── NewDialogBoxInstance dialogBoxNoPortrait  // 0x98
  └── NewDialogBoxInstance dialogBoxSystem      // 0xA0
```

`NewDialogBoxInstance` (TypeDefIndex 5056) is a MonoBehaviour that handles one
rendering slot. Its fields relevant to this function:

| Offset | Field | Type |
|--------|-------|------|
| 0x40 | `textfield` | TextMeshProUGUI |
| 0x48 | `textLocalizer` | TextLocalizer |
| 0x50 | `newDialogBoxNamePlate` | NewDialogBoxNamePlate |
| 0x58 | `portrait` | NewDialogBoxPortrait |
| 0x60 | `continueArrow` | NewDialogBoxContinueArrow |
| 0x68 | `backgroundImages` | List\<Image\> |
| 0x70 | `playerIndicator` | SinglePlayerPlusPlayerIndicator |
| 0x78 | `scrollingOutlineImage` | ScrollingOutlineImage |
| 0x80 | `owner` | NewDialogBox |
| 0x88 | `currentDialogBoxData` | DialogBoxData |
| 0x90 | `currentDialogTokenReplaceDatas` | List\<DialogTokenReplaceData\> |
| 0x98 | `updateMode` | AnimatorUpdateMode |

---

## Signature

```csharp
public void PlayDialog(
    DialogBoxData dialogBoxData,
    DialogBoxSettings dialogBoxSettings,
    List<DialogTokenReplaceData> dialogTokenReplaceDatas)
```

### IL2CPP parameters

| IL2CPP param | Logical name | Type |
|---|---|---|
| `param_1` | `this` | `NewDialogBoxInstance*` |
| `param_2` | `dialogBoxData` | `DialogBoxData*` |
| `param_3` | `dialogBoxSettings` | `DialogBoxSettings*` (struct, 160 bytes) |
| `param_4` | `dialogTokenReplaceDatas` | `List<DialogTokenReplaceData>*` |

---

## `param_3` — DialogBoxSettings struct layout (key offsets)

`param_3` is a 160-byte value-type struct (20 × 8-byte slots). Fields decoded from
usage:

| Slot index | Byte offset | Decoded field |
|---|---|---|
| 0, 1 | 0x00–0x0F | *scratch / re-read for each sub-call (Ghidra artefact)* |
| 4 | 0x20 | packed bools including `hidePortrait` (byte 1) and `hideCharacterName` (byte 2) |
| 6 | 0x30 | first byte = `useCustomCharacterName` flag |
| 7, 8 (lo) | 0x38–0x47 | `LocalizationId` for custom character name (16 bytes) |
| 8 (hi-DWORD) | 0x44–0x47 | `AnimatorUpdateMode updateMode` (4-byte enum) — stored to `this.updateMode` |
| 9 | 0x48 | *dialog sort / text localization context* |
| 10 | 0x50 | **`customPortrait : Sprite`** — Unity Object reference; nullable override |
| 11 | 0x58 | `customPortraitBackground : Sprite` — passed as background arg to SetPortrait |
| 14, 15 | 0x70–0x7F | packed flags; byte 2 of slot 15 = `forceLatinFont`/language flag |

---

## Execution flow

### 1. Boilerplate

IL2CPP write-barrier setup (incremental GC) and lazy type-metadata initialisation for
`Func<LocalizationId, string>`, `LocalizationId`, `Manager<NewDialogManager>.get_Instance()`,
`Manager<CharacterDefinitionManager>.get_Instance()`, `NewDialogBoxInstance.DialogBoxTextProcessor`,
`UnityEngine.Object`.

### 2. Store parameters into instance fields

```csharp
this.currentDialogBoxData = dialogBoxData;              // param_1[0x11] = param_2
this.currentDialogTokenReplaceDatas = dialogTokenReplaceDatas;  // param_1[0x12] = param_4
this.updateMode = dialogBoxSettings.updateMode;         // *(uint*)(param_1 + 0x98) = hi-DWORD of param_3[8]
```

### 3. Text localisation

```csharp
// Build Func<LocalizationId,string> from NewDialogManager's localisation table
var locFunc = NewDialogManager.Instance.CreateLocalizationFunc(...);
// Wire up this instance's text post-processor
NewDialogBoxInstance.DialogBoxTextProcessor(locFunc, this, textProcessorMethod);
// Localise dialog body text from DialogBoxData
if (this.textLocalizer != null)
    this.textLocalizer.LocalizeText(dialogBoxData.localizationId, locFunc);
```

*(The repetitive re-reads of `param_3` slots throughout the function are Ghidra
re-materialising the struct from the original stack pointer after each sub-call — not
actual redundant work.)*

### 4. Font-by-language (conditional)

```csharp
// dialogBoxSettings slot-15 byte-2 flag (forceLatinFont or similar)
if (settings.fontOverrideFlag)
    this.textLocalizer.SetFontByLanguage(...);
// Then: activate font override on the TextMeshProUGUI via vtable slot 0x5d/0x5e
```

### 5. Character-object visual effects (conditional)

`param_1[8]` (offset 0x40 = `textfield`) is used via vtable slots 0xfb/0xfc and
0x5f/0x60 — likely `SetCharacterState` / `ResetMaterial` calls on the TextMeshPro
component. If the backing field at `textfield + 0x198` (int at `pauVar12[0x32] + 8`)
is non-zero, a dirty-flag clear + vtable-slot call chain runs.

### 6. Continue arrow hidden

```csharp
if (this.continueArrow != null)
    this.continueArrow.SetActive(false);
```

### 7. Character definition lookup

```csharp
var cdManager  = CharacterDefinitionManager.Instance;          // FUN_1800149f0
var charDefId  = this.owner.currentCharacterDefinitionId;      // owner field +0x148
var charDef    = cdManager.GetCharacterDefinition(charDefId);  // ppppplVar7
```

`this.owner` is the `NewDialogBox` parent at offset 0x80.
`NewDialogBox.currentCharacterDefinitionId` is at 0x148 (`owner[0x29]`).

### 8. Portrait setup

Executed only when `charDef != null` (valid Unity object) and
`!settings.hidePortrait` (byte 1 of settings slot 4 is clear):

```csharp
// Base portrait from CharacterDefinitionManager
Sprite portraitSprite = cdManager.GetPortrait(charDef);

// customPortrait override — settings slot 10
var overrideSprite = settings.customPortrait;   // param_3[10]
if (overrideSprite != null /* Unity object validity check */)
    portraitSprite = overrideSprite;

// Background sprite — settings slot 11  
Sprite bgSprite = settings.customPortraitBackground;  // param_3[11]

this.portrait.SetPortrait(portraitSprite, bgSprite);  // NewDialogBoxPortrait$$SetPortrait
```

**This is the critical branch for the SeraiDefaultSkin mod** — see "Mod implications" below.

### 9. Name-plate setup

Executed when `charDef != null` and `!settings.hideCharacterName` (byte 2 of slot 4):

```csharp
LocalizationId nameLocId;
if (!settings.useCustomCharacterName)           // first byte of param_3[6]
{
    // From CharacterDefinition fields [9] and [10] (byte offsets +0x48..+0x57)
    nameLocId = charDef.characterNameLocId;
}
else
{
    // From settings slots 7 + 8-lo (packed LocalizationId, 16 bytes)
    nameLocId = settings.customCharacterName;
}
// Color from settings slots 6,7,8 (or 1.0f,1.0f,1.0f,1.0f if useCustomCharacterNameColor = false)
Color nameColor = settings.useCustomCharacterNameColor
    ? settings.customCharacterNameColor           // CONCAT44 from slots 7/8
    : Color.white;

this.newDialogBoxNamePlate.SetName(nameLocId, nameColor);
```

When `charDef == null` — the fallback name path uses a separate `NewDialogBoxNamePlate`
component reference and resolves an empty/default localization ID via
`Sabotage.Localization.LocalizationId_TypeInfo[0x17]`.

### 10. Background / outline colors

The final block sets per-channel color values on `this.scrollingOutlineImage`
(field `+0x78`, accessed as `param_1[0xf]`) using CharacterDefinition fields at offsets
`+0x58..+0x77` (`ppppplVar7[0xb..0xe]`). When no valid definition is resolved, the
code writes hardcoded constants:

```
0x3eaeaeaf3e40c0c1  →  r ≈ 0.189f, g ≈ 0.341f   (two floats packed)
0x3f8000003f139394  →  b ≈ 0.575f, a = 1.0f
```

This produces a muted teal/blue — the game's default "system character" colour. The
same two 128-bit RGBA values are written to `scrollingOutlineImage` fields at indices
0x22/0x23 (primary quad) and 0x24/0x25 (secondary quad).

---

## Reconstructed C# (abridged)

```csharp
public void PlayDialog(
    DialogBoxData dialogBoxData,
    DialogBoxSettings settings,
    List<DialogTokenReplaceData> tokenReplaceDatas)
{
    // 1. Store
    currentDialogBoxData  = dialogBoxData;
    currentDialogTokenReplaceDatas = tokenReplaceDatas;
    updateMode = settings.updateMode;

    // 2. Text
    var locFunc = NewDialogManager.Instance.CreateLocalizationFunc(dialogBoxData);
    SetupTextProcessor(locFunc);
    if (textLocalizer != null)
        textLocalizer.LocalizeText(dialogBoxData.localizationId, locFunc);
    if (settings.fontOverrideFlag && textLocalizer != null)
        textLocalizer.SetFontByLanguage();

    // 3. Character visual effects (dirty-flags on TextMeshPro)
    // ...

    // 4. Continue arrow hidden at start
    if (continueArrow != null)
        continueArrow.SetActive(false);

    // 5. Look up character definition
    var cdManager = CharacterDefinitionManager.Instance;
    var charDef   = cdManager.GetCharacterDefinition(owner.currentCharacterDefinitionId);
    if (charDef == null) goto FallbackNamePlate;

    // 6. Portrait
    if (!settings.hidePortrait)
    {
        Sprite portraitSprite = cdManager.GetPortrait(charDef);
        if (settings.customPortrait != null)
            portraitSprite = settings.customPortrait;     // baked override from PlayDialogNode
        portrait.SetPortrait(portraitSprite, settings.customPortraitBackground);
    }

    // 7. Name plate
    if (!settings.hideCharacterName)
    {
        LocalizationId nameId = settings.useCustomCharacterName
            ? settings.customCharacterName
            : charDef.characterNameLocId;
        Color nameColor = settings.useCustomCharacterNameColor
            ? settings.customCharacterNameColor
            : Color.white;
        newDialogBoxNamePlate.SetName(nameId, nameColor);
    }

    // 8. Background object game-object show/hide and color set
    // ...SetActive, SetText, SetColor via scrollingOutlineImage...
    return;

FallbackNamePlate:
    // NamePlate with empty LocalizationId and muted teal colour
    // ...
}
```

---

## Mod implications — SeraiDefaultSkin

### Normal portrait path (already fixed)

`cdManager.GetPortrait(charDef)` is called with ROBOT's `CharacterDefinition`
object. Because `Patch_CharacterDefinitionManager_OnCharacterDefinitionLoaded`
replaced ROBOT's `defaultPortrait` `AssetReferenceSprite` with a new wrapper that
points to DEFAULT's asset GUID (+ SubObjectName), the Addressables runtime loads and
returns DEFAULT's portrait sprite. This path requires no additional patching.

### customPortrait override path (new patch target)

`PlayDialogNode` serialises a `GraphVariable<Sprite> customPortrait` (evaluated at
runtime to a concrete `Sprite` reference) into `DialogBoxSettings.customPortrait`
(struct slot 10, byte offset 0x50). If a cutscene designer explicitly set this field on
a specific dialogue node, the sprite object is baked into the scene asset and is
returned directly — bypassing `CharacterDefinitionManager.GetPortrait` entirely.
A ROBOT-variant sprite baked this way cannot be intercepted by patching the
CharacterDefinition fields.

The override sprite replaces `portraitSprite` **before** `SetPortrait` is called:

```
GetPortrait(robotCharDef)   →  defaultPortrait (DEFAULT sprite, already fixed)
customPortrait override      →  ROBOT sprite (baked into scene, NOT fixed by CDef patch)
↓
portrait.SetPortrait(sprite, bg)   ←  Patch_NewDialogBoxPortrait_SetPortrait intercepts here
```

`Patch_NewDialogBoxPortrait_SetPortrait` (added in SeraiDefaultSkinPatches.cs)
intercepts at the final `SetPortrait` call regardless of which path produced the
sprite, matching by Sprite name against the saved ROBOT portrait SubObjectNames.

### customPortraitBackground

Passed as-is as the second arg to `SetPortrait`. If a node has a ROBOT background
sprite baked in, it would also need patching. This has not been observed in practice;
`NewDialogBoxPortrait.SetPortrait(Sprite portrait, Sprite background)` is where a
background patch would go if needed.

---

## Summary table

| Step | What happens | Fixed by |
|---|---|---|
| `GetPortrait(robotCharDef)` | Returns DEFAULT portrait (ROBOT's AssetRef now points to DEFAULT GUID) | `Patch_CharacterDefinitionManager_OnCharacterDefinitionLoaded` |
| `settings.customPortrait` non-null override | Replaces portrait sprite with baked ROBOT sprite | `Patch_NewDialogBoxPortrait_SetPortrait` (name-match intercept) |
| Name plate colors | Read from ROBOT CharacterDefinition color fields | `CopyVisualFields` copies `characterColor` etc. |
