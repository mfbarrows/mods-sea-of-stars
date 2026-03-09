# `SolarRainCombatMove.OnSunballReady` — Analysis

## Context

`SolarRainCombatMove` is the **Solar Rain** combo move (Soonrang + Garl).
Sunboy charges the sunball via `SunboyShootQTESunballState` (the QTE hold-A
input). When the charge completes — either by player release or by the step timer
expiring — `BeginShoot` fires `onSunballReady`, which is wired to this method.

`OnSunballReady` is therefore the **bridge** between the QTE charge state machine
and the SolarRain move's throw logic. It runs on the `SolarRainCombatMove`
instance, not on the state.

---

## Pointer Arithmetic Key

`param_1` is `longlong*` — the base of `SolarRainCombatMove`.
`param_1[N]` = `*(longlong*)(base + N×8)`.
Byte-level accesses use cast to `char*` / `undefined1*`.

| Expression | Offset | C# field | Type |
|---|---|---|---|
| `param_1 + 0x1E` → byte write | `0xF0` | `sunballReady` | bool |
| `*(char*)(base + 0xF1)` | `0xF1` | `garlInPosition` | bool |
| `(char)param_1[0x1E]` | `0xF0` | `sunballReady` (re-read) | bool |
| `param_1[0x21]` | `0x108` | `sunboy` | SunboyCombatActor* |
| `*(longlong**)(sunboy + 0x80)` | `sunboy+0x80` | `sunboy.stateMachine` | StateMachine |

`StateMachine.GetState<SunboyShootQTESunballState>()` — generic state lookup;
returns the `SunboyShootQTESunballState` instance currently held by Sunboy's
state machine.

---

## Annotated C# Reconstruction

```csharp
private void OnSunballReady()   // registered as the onSunballReady Action delegate
{
    // ── Mark that the sunball charge is complete ──────────────────────────────
    sunballReady = true;   // +0xF0

    // ── Gate: both preconditions must be met before throwing ─────────────────
    // garlInPosition  (+0xF1) — set by OnGarlPositioned() when Garl arrives
    // sunballReady    (+0xF0) — just set above
    // Either may arrive first; this check fires the throw only when both are true.
    if (!garlInPosition || !sunballReady)
        return;

    // ── Retrieve the live QTE state from Sunboy's state machine ──────────────
    if (sunboy == null) throw NullReferenceException();
    StateMachine sm = sunboy.stateMachine;   // sunboy+0x80
    if (sm == null) throw NullReferenceException();

    SunboyShootQTESunballState qteState =
        sm.GetState<SunboyShootQTESunballState>();

    if (qteState == null) throw NullReferenceException();

    // ── Trigger the actual throw ──────────────────────────────────────────────
    qteState.ThrowSunball();
}
```

---

## Two-Condition Latch

The method exists to synchronise two asynchronous events:

| Event | Field set | Method |
|---|---|---|
| Garl arrives at position | `garlInPosition = true` (+0xF1) | `OnGarlPositioned` |
| Sunball charge completes | `sunballReady = true` (+0xF0) | `OnSunballReady` (this) |

`ThrowSunball` fires only when **both** flags are true. Whichever arrives second
triggers it. This matches `TryThrowSunball` (VA `0x18053E6B0`), which is likely
the same guard expressed as a shared helper.

---

## What `SunboyShootQTESunballState.ThrowSunball` Does

From the dump signature: `public void ThrowSunball(float normalizedTime)`.
Here it is called with no explicit argument (the call is
`SunboyShootQTESunballState$$ThrowSunball(pauVar2, 0, ...)`) — defaulting to
`normalizedTime = 0`. This tells the animation system to begin the throw
animation from the start of the clip rather than at a mid-point.

---

## Mod Implications

**This method is not a patch target.**

The `onSunballReady` Action is invoked from `BeginShoot` (as confirmed by the
`BeginShoot` analysis). The auto-release patch writes `playerInputDone = true`,
`playerQTEResult = SuccessPerfect`, and `-1.0f` to `currentChargeStepDuration`.
The next `UpdateCharging` calls `BeginShoot`, which calls `onSunballReady`, which
calls this method, which calls `ThrowSunball` — exactly the same path as a
natural player release. No additional patching is needed here.

The only new information relevant to the mod:

- **`garlInPosition`** must already be `true` for the throw to proceed. If the
  auto-release fires before Garl arrives, `sunballReady` will be set but
  `ThrowSunball` will not be called until `OnGarlPositioned` fires later —
  which is also fine, since `TryThrowSunball` / `OnSunballReady` will then
  trigger it on that path. The latch handles the race automatically.
