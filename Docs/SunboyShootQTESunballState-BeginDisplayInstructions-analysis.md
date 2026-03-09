# `SunboyShootQTESunballState.BeginDisplayInstructions` — Analysis

## Summary

**This method is pure UI.** It types the "hold A to charge" instruction text
onto the BattleScreen using `TextTyper`. It performs **zero field writes** on
`SunboyShootQTESunballState` and is **completely irrelevant to the auto-release mod**.

---

## Pointer Arithmetic Key

`param_1` = `this` as `ulonglong*`, so `param_1[n]` = `base + n×8`.

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1[0x12]` | `+0x90` | `instructionsLocId` (LocalizationId, low 8 bytes) |
| `param_1[0x13]` | `+0x98` | `instructionsLocId` (LocalizationId, high 8 bytes) |
| `param_1[0x22]` | `+0x110` | `sunboy` (SunboyCombatActor*) |
| `sunboy + 0x1A0` | — | `sunboy.player` (Player*) |
| `player + 0x38` | — | `player.controllers` (Rewired.Player.ControllerHelper) |

`LocalizationId` is a 16-byte value type spanning `+0x90`–`+0x9F`. The null guard
checks `param_1[0x12] != 0` (the id itself) and `*(int*)(param_1[0x12] + 0x10) != 0`
(a length/valid flag inside the struct), then repeats for `param_1[0x13]` — this is
just IL2CPP's standard null-plus-length check on the two halves of a managed struct
with an embedded string pointer.

---

## Annotated C# Reconstruction

```csharp
private void BeginDisplayInstructions()
{
    // ── Guard: both halves of LocalizationId must be valid ───────────────────
    // If instructionsLocId is unset, silently skip UI (no crash).
    if (instructionsLocId is empty/null)
        return;

    // ── Get the BattleScreen view ─────────────────────────────────────────────
    UIManager ui         = Manager<UIManager>.get_Instance();
    BattleScreen screen  = ui.GetView<BattleScreen>();
    if (screen == null) { FatalError(); return; }

    // ── Show stat panel (clears/prepares the text area) ──────────────────────
    screen.ShowStatInfos();   // GameMenuNewCharacterSection$$ShowStatInfos

    // ── Resolve the Rewired controller for this player ───────────────────────
    Player player = (sunboy.player != null)
                    ? sunboy.player
                    : InputManager.Instance.FirstPlayer;

    Rewired.Controller controller =
        player.controllers.GetLastActiveController();

    // ── Type the instruction text (e.g. "Hold [A] to charge") ────────────────
    TextTyper typer = screen.someTextTyper;   // at BattleScreen + 0x20
    if (typer == null) { FatalError(); return; }

    // Hash 0x3d23d70a is the localised-string type token / method pointer.
    typer.TypeText(instructionsLocId, /* hash */ 0x3d23d70a, player, controller);
}
```

---

## Mod Relevance

None. This method:

- Reads `instructionsLocId` (+0x90) and `sunboy` (+0x110) — both read-only here
- Writes nothing on `SunboyShootQTESunballState`
- Fires once at state entry, before any charging begins

It need not appear in any patch and does not interact with the auto-release logic.
