# NewDialogBoxPortrait$$SetPortrait — Ghidra Analysis

## Signature
```csharp
public void SetPortrait(Sprite portraitSprite, Sprite background)
```

IL2CPP params:
- `param_1` — `this` (NewDialogBoxPortrait)
- `param_2` — `portraitSprite` (Sprite)
- `param_3` — `background` (Sprite)

---

## NewDialogBoxPortrait Field Layout
Inferred from pointer arithmetic on `param_1`:

| Offset | Field | Type |
|--------|-------|------|
| `[3]` | `portraitImage` | `UnityEngine.UI.Image` |
| `[4]` | `backgroundImage` | `PortraitBackgroundImage` |
| `[5]` | default background sprite | `Sprite` (fallback when `background` param is null) |

---

## Execution Flow

### Phase 1 — Resolve background database
```
FUN_1800149f0(this, portraitSprite, background, ...)
  → lVar4  (some manager/database object)
  → lVar4 + 0x38 dereferenced → CharacterPortraitBackgroundDatabase (or field thereof)
```
If `lVar4 == 0` or `*(lVar4 + 0x38) == 0` → **null check fail → goto error/return**.

Fields used from `lVar4`:
- `lVar4 + 0x18` — default `CharacterPortraitBackgroundInfo*` (used when portrait not in dict)
- `lVar4 + 0x28` — `Dictionary<string, CharacterPortraitBackgroundInfo>` for per-portrait lookup

---

### Phase 2 — Background info lookup by portrait sprite NAME

```
if (portraitSprite == null || portraitSprite is Unity-null):
    backgroundInfo = *(lVar4 + 0x18)          // use default entry
else:
    key = portraitSprite.name                  // UnityEngine.Object$$get_name
    dict = *(lVar4 + 0x28)
    if dict == null → goto error
    found = dict.TryGetValue(key, out info)
    if !found:
        backgroundInfo = *(lVar4 + 0x18)      // fall back to default entry
    else:
        if info == null → goto error
        backgroundInfo = info[4]              // CharacterPortraitBackgroundInfo field [4]
```

**`portraitSprite.name` is the lookup key.** If the name is not registered in the
dictionary, the method silently falls back to the default background entry — different
colours, different material params.

---

### Phase 3 — Set portrait image sprite
```
portraitImage = this[3]
materialParams = backgroundInfo[3]        // used later for material push

if portraitImage == null → goto error
portraitImage.sprite = portraitSprite     // Image$$set_sprite
```

---

### Phase 4 — Resolve background sprite
```
backgroundImage = this[4]

if background == null || background is Unity-null:
    resolvedBackground = this[5]          // field [5]: default/fallback background sprite
else:
    resolvedBackground = background       // use the passed-in background param
```

The `background` **parameter** controls which **Sprite** is displayed.
The **dictionary lookup** by portrait name controls the **material params** (colours etc.)
applied to `backgroundImage`.

---

### Phase 5 — Set background image + push material params
```
if backgroundImage != null:
    backgroundImage.sprite = resolvedBackground        // Image$$set_sprite

    if materialParams != null:
        count = *(uint *)(materialParams + 3)          // Il2CppArray count
        // Copy up to 5 key/value pairs from materialParams into backgroundImage:
        backgroundImage[0x22] = materialParams[4]
        backgroundImage[0x23] = materialParams[5]
        if count >= 2:
            backgroundImage[0x24] = materialParams[6]
            backgroundImage[0x25] = materialParams[7]
        if count >= 3:
            backgroundImage[0x26] = materialParams[8]
            backgroundImage[0x27] = materialParams[9]
        if count >= 4:
            backgroundImage[0x28] = materialParams[10]
            backgroundImage[0x29] = materialParams[11]
        if count >= 5:
            backgroundImage[0x2a] = materialParams[12]
            backgroundImage[0x2b] = materialParams[13]

        PortraitBackgroundImage$$PushMaterialParams(backgroundImage, ...)
        // Two further vtable calls on backgroundImage
```

`materialParams` is an `Il2CppArray`-like structure; the paired fields appear to be
`(key, value)` entries for shader/material property overrides.

---

## Key Finding for the Mod

`SetPortrait` has **two independent sources of "background"**:

| What | Source | Driven by |
|------|---------|-----------|
| Background **Sprite** | `background` param or `this[5]` fallback | Caller (e.g. `PlayDialog`) |
| Background **material params** (colours etc.) | `dict[portraitSprite.name][3]` | **Portrait sprite name** |

When we loaded a custom disk PNG without setting `sprite.name`, the name was empty/generic,
the dictionary lookup missed, and the default material params were applied — producing the
"different background" (wrong colour scheme).

**Fix already applied:** `sprite.name = "dialog-portrait-Serai-Determined"` (the DEFAULT
sprite name) so the dictionary finds the correct `CharacterPortraitBackgroundInfo` entry and
uses the matching material params. The background colours now match the DEFAULT portrait.

---

## Implications for Custom Portraits

For any custom disk-loaded sprite to display with the correct background:
1. **`sprite.name` must match an existing key in the background database dictionary.**
2. The safest choice is the name of the DEFAULT equivalent (e.g. `dialog-portrait-Serai-Determined`).
3. There is no way to register a new entry into the background dictionary at runtime without
   patching the dictionary population code — so custom portraits that have no DEFAULT
   equivalent (e.g. Moved, GoldenPelican) should reuse the name of the closest DEFAULT
   expression to get sensible background params.
