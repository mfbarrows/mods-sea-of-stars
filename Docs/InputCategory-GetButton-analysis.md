# `InputCategory.GetButton` — Decompilation Analysis

## Summary

`GetButton` is a thin three-gate wrapper around `Rewired.Player.GetButton`.  
It returns `false` if the platform isn't available, the input is blocked, or
the input is consumed; otherwise it delegates to Rewired.

---

## Pointer Arithmetic Key

`param_1` is `undefined8*` — each slot is 8 bytes.  
IL2CPP object header occupies `param_1[0]` (vtable) and `param_1[1]` (monitor),
so fields begin at `param_1[2]` = `+0x10`.

| Expression | Byte offset | Field (from `InputCategory` dump) |
|---|---|---|
| `param_1[2]` | `+0x10` | `blockedInputs` (`HashSet<string>`) |
| `param_1[3]` | `+0x18` | `consumedInputs` (`HashSet<string>`) |
| `param_1[4]` | `+0x20` | `player` — passed directly to `Rewired.Player.GetButton` |

---

## Annotated C# Reconstruction

```csharp
public bool GetButton(string button)
{
    // ── Gate 1: platform availability ────────────────────────────────────────
    // FUN_180016c20 resolves the PlatformManager singleton; +0x38 on it is the
    // concrete IPlatformImpl.  If absent (headless / pre-init) return false.
    var platformImpl = Manager<PlatformManager>.get_Instance().platformImpl;  // +0x38
    if (platformImpl == null || !(platformImpl is IPlatformImpl))
        return false;

    // ── Gate 2: block list ────────────────────────────────────────────────────
    if (blockedInputs != null && blockedInputs.Contains(button))   // +0x10
        return false;

    // ── Gate 3: consume list ─────────────────────────────────────────────────
    if (consumedInputs != null && consumedInputs.Contains(button)) // +0x18
        return false;

    // ── Delegate to Rewired ───────────────────────────────────────────────────
    return Rewired.Player.GetButton(player, button);   // +0x20
}
```

---

## Findings Relevant to the Mod

### 1. The button name is "Bubble", not "Attack"

The `StringLiteral_1581 = "Attack"` assumption from the `UpdateIn` decompilation
was incorrect. Runtime logs show `GetButton` is polled exclusively with
`button = "Bubble"` during the QTE.  The UpdateIn pseudocode must use a
different string literal index for Soonrang vs the base Sunboy move.

### 2. Three rotating instances, none matching our stored target

Logs show three `InputCategory` instances (`0x...F440`, `0x...F280`, `0x...F100`)
polled for "Bubble" every frame. Our stored pointer (`0x272B1C4C370`) matches
none of them. Root cause: we read `PlayerCombatActor.playerInputs` (+0x1A8)
off the `sunboyCombatActor` pointer (+0x110), but the three polled instances
are not at that address. Possible causes:
  - The pointer at `+0x110` in the state is not `SunboyCombatActor` but a
    different object — worth verifying with a StateEnter log of the raw ptr.
  - `playerInputs` at `+0x1A8` is correct in `PlayerCombatActor` but the three
    instances are a different `InputCategory` subclass (e.g. `UIInputs`).
  - The game polls Rewired through a different input aggregator, not through
    the specific actor's `PlayerInputs`.

### 3. `blockedInputs` is a simpler intercept surface

Because Gate 2 checks `blockedInputs.Contains(button)` **before** the Rewired
call, adding `"Bubble"` to `blockedInputs` on the target instance would cause
`GetButton` to return `false` regardless of physical input — with zero Harmony
involvement. Removing it would restore normal input. This is a viable
alternative to patching `GetButton` at all.

The managed API already exposes this:
```csharp
inputCategory.BlockInput("Bubble");    // adds to blockedInputs
inputCategory.UnblockInput("Bubble"); // removes from blockedInputs
```

### 4. "Consume" differs from "Block"

`consumedInputs` also returns `false`, but the existing `ConsumeInput(string)`
method adds to it. Unlike blocking, consuming is frame-scoped in practice
(cleared elsewhere each frame). Not useful for sustained suppression.

---

## New Approach: Use BlockInput/UnblockInput Instead of GetButton Patch

Instead of patching `GetButton`, we can call `BlockInput("Bubble")` on the
correct `InputCategory` and rely on Gate 2. The Prefix on `GetButton` goes away
entirely.

The remaining problem is identifying **which** of the three polled instances to
block. Since all three are foreign to our stored target, the correct path is:

1. Add a StateEnter log of the raw `sunboyPtr` and the ptr we read at `+0x1A8`.
2. Compare against the three polled instance addresses to identify which field
   actually holds the polled `PlayerInputs`.

Alternatively, because all three instances cycle "Bubble" together (same
timestamps, same button), **blocking "Bubble" on all active InputCategory
instances** during the QTE window would work — none of them are Sunboy-specific
(they appear to be party-wide or per-controller slots). That matches the
Soonrang QTE lore: Soonrang controls the sunball, not the player character.

---

## Field Map for `InputCategory` (Confirmed)

| Offset | Type | Field |
|---|---|---|
| `+0x10` | `HashSet<string>` | `blockedInputs` |
| `+0x18` | `HashSet<string>` | `consumedInputs` |
| `+0x20` | `Rewired.Player` | `player` |
