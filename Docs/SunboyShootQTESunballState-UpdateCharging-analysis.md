# `SunboyShootQTESunballState.UpdateCharging` — Analysis

## Pointer Arithmetic Key

`param_1` is typed `undefined1 (*)[16]` — an array of 16-byte blocks.
`param_1[N]` = `base + N×0x10`. Sub-byte offsets within a block are accessed as `param_1[N] + k`.

| Expression | Offset | C# field |
|---|---|---|
| `param_1[5] + 8` | `0x58` | `chargeStepDuration` (float) |
| `param_1[5] + 0xC` | `0x5C` | `qteFullChargeStepDuration` (float) |
| `param_1[6] + 8` | `0x68` | `playMaxReachedSFX` (Event) |
| `param_1[7]` | `0x70` | `stopMaxReachedSFX` (Event) |
| `param_1[0xB]` | `0xB0` | `sunballMaxLevel` (int) |
| `param_1[0xD]` | `0xD0` | `sunballCharge` (float) |
| `param_1[0xE]` | `0xE0` | `playerQTEResult` (QTEResult struct) |
| `param_1[0x11] + 8` | `0x118` | `sunballProjectileInstance` (SunballProjectile*) |
| `param_1[0x11]` ptr deref `+ 0x1A8` | — | `inputCategory` on the sunboy actor |
| `param_1[0x12] + 4` | `0x124` | `shootOnNextChargeStep` (bool) |
| `param_1[0x12] + 8` | `0x128` | `currentChargeStepDuration` (float) — the step countdown |
| `param_1[0x12] + 0xC` | `0x12C` | `sunballChargeDuration` (float) — normalising divisor |
| `param_1[0x13] + 4` | `0x134` | `playerInputDone` (bool) |
| `param_1[0x14]` | `0x140` | `additionalPlayersQTEState` (List<>) |

For SunballProjectile (`proj`):
- `*(int *)(proj[0xC] + 0xC)` = `proj_base + 0xCC` = `SunballProjectile.level` (int)

---

## Annotated C# Reconstruction

```csharp
private void UpdateCharging()
{
    // ── Phase 1: Always count down the step timer ────────────────────────────
    currentChargeStepDuration -= Time.deltaTime;          // +0x128

    // ── Phase 2: Update sunballCharge while timer is running ─────────────────
    SunballProjectile proj = sunballProjectileInstance;   // +0x118

    if (!shootOnNextChargeStep)                           // +0x124
    {
        // Rising or holding phase.
        // Charge is only accumulated when still below max level.
        if (proj.level < sunballMaxLevel)
            sunballCharge += Time.deltaTime / sunballChargeDuration;   // +0xD0 / +0x12C
        // UpdateSunballCharge() is NOT called here — visuals update only at step
        // boundaries or when shootOnNextChargeStep is active.
    }
    else
    {
        // Past-peak / falling phase: charge decreases per frame.
        sunballCharge -= Time.deltaTime / sunballChargeDuration;
        UpdateSunballCharge();   // propagate falling charge to light/audio/pullback
    }

    // ── Phase 3: Step timer expired ───────────────────────────────────────────
    if (currentChargeStepDuration <= 0.0f)
    {
        // Short-circuit if the player already released and co-op is settled.
        if (playerInputDone && !IsWaitingForAdditionalPlayers())
        {
            BeginShoot();
            return;
        }

        if (shootOnNextChargeStep)
        {
            // Falling step expired — the peak window was missed entirely.
            // Normalise charge to current level fraction and fire the weakened shot.
            sunballCharge = (float)proj.level / (float)sunballMaxLevel;
            UpdateSunballCharge();
            BeginShoot();   // ← fires a sub-max-level sunball
            return;
        }

        if (proj.level == sunballMaxLevel)
        {
            // ── Peak step expired with no release ───────────────────────────
            // For single-step Soonrang (maxLevel ≤ 2): go straight to shoot.
            if (sunballMaxLevel <= 2)
            {
                BeginShoot();
                return;
            }

            // Check co-op additional players; if any are still pending, wait.
            foreach (SunballAdditionalPlayersQTEState s in additionalPlayersQTEState)
                if (s is done/succeeded) { BeginShoot(); return; }

            // No one released in time — begin the level drop.
            stopMaxReachedSFX.Post(proj.gameObject);
            proj.SetLevel(proj.level - 1);          // 4 → 3
            shootOnNextChargeStep = true;            // gate: charge now falling
            // Reset timer. currentChargeStepDuration is negative (overshoot);
            // adding it to chargeStepDuration corrects for the overshoot.
            currentChargeStepDuration = chargeStepDuration + currentChargeStepDuration;
        }
        else
        {
            // ── Normal upward step boundary ──────────────────────────────────
            proj.SetLevel(proj.level + 1);          // 1→2, 2→3, or 3→4

            if (proj.level == sunballMaxLevel)
            {
                // *** Just reached peak level ***
                playMaxReachedSFX.Post(proj.gameObject);
                sunballCharge = 1.0f;               // pin charge visuals to maximum
                UpdateSunballCharge();
                // Load the peak HOLD WINDOW timer (typically shorter than a normal step).
                currentChargeStepDuration = qteFullChargeStepDuration;
                // shootOnNextChargeStep stays false — we're in the sweet spot.
            }
            else
            {
                // More steps to go — roll over with overshoot correction.
                currentChargeStepDuration += chargeStepDuration;
            }
        }
    }

    // ── Phase 4: Button-release detection (runs every frame) ─────────────────
    InputCategory input = sunboy.inputCategory;   // ptr at sunboy+0x1A8
    if (input != null)
    {
        bool buttonHeld = InputCategory.GetButton(input, "Attack");   // StringLiteral_1581

        if (!buttonHeld && !playerInputDone)
        {
            if (proj.level == sunballMaxLevel)
            {
                // *** SUCCESS: released at peak ***
                // Record: QTEResult.player = Player.get_Player(sunboy)
                playerQTEResult = new QTEResult { player = sunboy.Player };   // +0xE0/+0xE8
            }
            // Whether at max or not, mark input done (records a miss if not at max).
            // Note: playerQTEResult.owner (+0xE8) is NOT re-written here;
            // it was already set by StateEnter to the correct Player reference.
            playerInputDone = true;                // +0x134

            if (!IsWaitingForAdditionalPlayers())
            {
                BeginShoot();
                return;
            }
        }
    }
}
```

---

## The Two Timer Variables Explained

| Field | Role | When loaded |
|---|---|---|
| `chargeStepDuration` (+0x58) | How long each upward step lasts (1→2, 2→3, 3→4) AND how long the falling step lasts (4→3) | Every step boundary except reaching max |
| `qteFullChargeStepDuration` (+0x5C) | The **sweet-spot hold window** at max level | Only when `proj.level` transitions to `sunballMaxLevel` |

The "4→3" fallback uses `chargeStepDuration`, not `qteFullChargeStepDuration`. So the sweet-spot window equals exactly `qteFullChargeStepDuration` seconds.

---

## Success vs. Failure Conditions

| Condition | Outcome |
|---|---|
| Button released while `level == sunballMaxLevel` | **Success** — `playerQTEResult` recorded with player, BeginShoot fires max-power shot |
| `qteFullChargeStepDuration` expires with no release | Level drops to `sunballMaxLevel - 1`, `shootOnNextChargeStep = true`, charge starts falling |
| Falling step timer expires | BeginShoot fires with `level / maxLevel` normalised charge — a weaker shot |
| Button released at any level below max | `playerInputDone = true` but `playerQTEResult` is NOT populated — implies a miss/fail |

---

## Mod Strategy: Auto-Release at Peak

**Target**: detect the frame `proj.level == sunballMaxLevel` first becomes true and immediately record a release, before `qteFullChargeStepDuration` can expire.

### What to write (all on `SunboyShootQTESunballState`)

| Field | Offset | Value | Effect |
|---|---|---|---|
| `playerInputDone` | `+0x134` | `true` | Marks input done; Phase 4 won't re-run |
| `currentChargeStepDuration` | `+0x128` | `-1.0f` | Forces timer-expired branch to fire next frame |
| `playerQTEResult.player` | `+0xE8` | `sunboy.Player` ptr | Tells the shot evaluator this was a max-charge release |

Do NOT set `shootOnNextChargeStep` (+0x124) — that is the "missed the window" flag. Writing it would make the next frame's timer expiry take the falling-shot path instead of the success path.

### Patch point

**Prefix on `SunboyShootQTESunballState.UpdateCharging`** (private — target by string name)
or equivalently a **Postfix on `StateExecute`** (public, slot 15, VA `0x180B26850`).

Condition to check before acting:
```csharp
unsafe bool ShouldAutoRelease(SunboyShootQTESunballState instance)
{
    SunballProjectile proj =
        *(SunballProjectile*)((byte*)instance.Pointer + 0x118);
    if (proj == null) return false;

    int level    = *(int*)((byte*)proj.Pointer + 0xCC);
    int maxLevel = *(int*)((byte*)instance.Pointer + 0xB0);
    bool done    = *(bool*)((byte*)instance.Pointer + 0x134);
    bool falling = *(bool*)((byte*)instance.Pointer + 0x124);   // shootOnNextChargeStep

    return level == maxLevel && !done && !falling;
}
```

### Writes when condition fires

```csharp
unsafe void AutoRelease(SunboyShootQTESunballState instance)
{
    byte* b = (byte*)instance.Pointer;

    // 1. Upgrade result: StateEnter set FailDidNoPress(4), we need SuccessPerfect(0)
    //    Owner (+0xE8) was pre-set by StateEnter — do NOT overwrite it.
    *(int*)(b + 0xE0) = 0;

    // 2. Gate Phase 4 — button may still be held but playerInputDone bypasses it
    *(bool*)(b + 0x134) = true;

    // 3. Force the step timer to expire → BeginShoot fires next frame
    *(float*)(b + 0x128) = -1.0f;
}
```

### Next-frame resolution

On the very next `UpdateCharging`:
- Phase 1: `currentChargeStepDuration` = `-1.0 - deltaTime` (still negative)
- Phase 2: `!shootOnNextChargeStep` + `level == maxLevel` → accumulates charge but UpdateSunballCharge skipped (fine)
- Phase 3: timer <= 0, `playerInputDone = true`, `!IsWaitingForAdditionalPlayers()` → **`BeginShoot()`** fires with a max-level projectile

---

## Outstanding: `playerQTEResult` importance

Whether writing `playerQTEResult` (+0xE0) matters depends on what `BeginShoot` or `HasQTESuccess` reads from it. The two remaining functions worth decompiling:

1. **`HasQTESuccess`** (VA `0x180B271D0`) — determines the actual success flag passed downstream; may check `playerQTEResult` vs a null/default state
2. **`BeginShoot`** (VA `0x180B25EC0`) — may read `playerQTEResult.player` to determine damage multiplier or SFX variant

If neither reads the struct, the player-ptr write is unnecessary and the two-field patch (`playerInputDone` + `currentChargeStepDuration`) is sufficient.
