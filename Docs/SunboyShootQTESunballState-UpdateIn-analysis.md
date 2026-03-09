# `SunboyShootQTESunballState.UpdateIn` — Analysis

## Summary

`UpdateIn` is the per-frame update for the `EShootSunballStep.In (= 0)` phase.
It polls the Attack button and synchronises a "pull sunball toward body" intro
animation to how long the button is held. When the animation completes, it:
- Hides the instruction text
- Calls `SpawnSunball()` (spawns the projectile, sets `sunballProjectileInstance`)
- Sets `currentStep = Charging (1)` → `StateExecute` switches to `UpdateCharging`

**This is the gate the auto-start mod must bypass.**

---

## Pointer Arithmetic Key

`param_1` is typed `longlong *******`, so `param_1[n]` = `*(base + n×8)` and
`param_1 + n` as a pointer = `base + n×8` (byte-addressed when cast).

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1[0x22]` | `+0x110` | `sunboy` (SunboyCombatActor*) |
| `param_1[0x22][0x35]` | `sunboy + 0x1A8` | `sunboy.playerInputs` (PlayerInputs*) |
| `param_1[0x22][0xf]` | `sunboy + 0x78` | `sunboy.animator` (Animator object-ref) |
| `*(char *)(param_1 + 0x1e)` | `+0xF0` | `inWaitingForInput` (bool) |
| `*(undefined4*)(param_1 + 0x24)` | `+0x120` | `currentStep` (EShootSunballStep) |
| `*(undefined4*)(param_1 + 0x25)` | `+0x128` | `currentChargeStepDuration` (float) |
| `*(undefined4*)(param_1 + 0xb)` | `+0x58` | `chargeStepDuration` (float) |

`StringLiteral_1581` = the string `"Attack"` — confirmed as the button name
passed to `InputCategory.GetButton`.

---

## `EShootSunballStep` Enum (Confirmed from Dump)

```csharp
private enum EShootSunballStep
{
    In       = 0,   // UpdateIn phase (this method)
    Charging = 1,   // UpdateCharging phase
    Shoot    = 2,   // Post-BeginShoot phase
}
```

---

## Annotated C# Reconstruction

```csharp
private void UpdateIn()
{
    // ── Guard ────────────────────────────────────────────────────────────────
    if (sunboy == null || sunboy.playerInputs == null) { FatalError(); return; }

    // ── Poll Attack button ────────────────────────────────────────────────────
    bool buttonHeld = sunboy.playerInputs.GetButton("Attack");   // StringLiteral_1581

    // ── Sub-state: inWaitingForInput (= returned to idle after a cancel) ──────
    // This sub-state is only active after the player released the button while
    // the intro animation was still playing (not yet at 100%).
    if (inWaitingForInput)   // +0xF0
    {
        if (!buttonHeld) return;   // Idle: waiting. Come back next frame.

        // Button just pressed again from idle.
        // Re-initialise the intro pose: play the intro anim, freeze, clear flag.
        sunboy.animator.Play(SunboyAnims.holdIntroHash, -1);
        sunboy.animator.Update(0f);
        sunboy.animator.speed = 0f;
        inWaitingForInput = false;   // fall through to animation-driven section
    }

    // ── Animation-driven section ──────────────────────────────────────────────
    float dt        = Time.deltaTime;
    float clipTime  = AnimatorExtension.GetCurrentClipTime(sunboy.animator);
    float clipLen   = AnimatorExtension.GetCurrentClip(sunboy.animator).length;

    if (!buttonHeld)
    {
        // ── Button released: reverse/undo the intro animation ─────────────────
        float newTime = (clipTime - dt) / clipLen;   // step backward

        if (newTime <= 0f)
        {
            // Reversed all the way to start — go back to idle, start waiting
            sunboy.animator.Play(PlayerAnims.idleHash, -1);
            sunboy.animator.speed = 1f;
            inWaitingForInput = true;   // +0xF0
            return;
        }
        else
        {
            // Still reversing — continue playing intro backward
            sunboy.animator.Play(SunboyAnims.holdIntroHash, 0);
            return;
        }
    }
    else
    {
        // ── Button held: advance the intro animation ──────────────────────────
        float progress = Mathf.Min((clipTime + dt) / clipLen, 1f);
        sunboy.animator.Play(SunboyAnims.holdIntroHash, 0);

        if (progress < 1f)
            return;   // still animating — keep holding

        // ── Animation complete: begin charging ────────────────────────────────

        // Dismiss instruction text
        BattleScreen screen = UIManager.Instance.GetView<BattleScreen>();
        screen.instructionTyper.OnOutDone();   // fades/hides the "Hold A" text

        // Play "charge ready" animation and unfreeze animator
        sunboy.animator.Play(SunboyAnims.chargeReadyHash, 0);
        currentStep = EShootSunballStep.Charging;      // +0x120 = 1
        sunboy.animator.speed = 1f;

        // Load the first charge step timer
        currentChargeStepDuration = chargeStepDuration;  // +0x128 = +0x58

        // *** CRITICAL: spawns the SunballProjectile, sets sunballProjectileInstance ***
        SpawnSunball();

        return;
    }
}
```

---

## What `SpawnSunball` Must Do (Inferred)

`SpawnSunball` (+VA `0x180B25C10`) is the final action of `UpdateIn`. Based on
what upstream code requires by the time `UpdateCharging` first runs:

- Instantiate the `sunballProjectilePrefab` (+0x88)
- Store the result in `sunballProjectileInstance` (+0x118)
- Call `proj.SetLevel(1)` or similar to initialise level to 1
- Possibly set `sunballChargeDuration` on the projectile

Without `sunballProjectileInstance` being set, every `UpdateCharging` and
`BeginShoot` call that reads `+0x118` will null-check and FatalError. **SpawnSunball
must be called** for the charge phase to work at all.

---

## `SunboyAnims` Hash Indices (Partially Resolved)

All hashes are stored in the `SunboyAnims` static class's fields:

| Access | Likely name | Usage |
|---|---|---|
| `SunboyAnims_TypeInfo[0x17] + 1` | `holdIntroHash` | The "pull sunball to body" intro anim |
| `SunboyAnims_TypeInfo[0x17] + 0xC` | `chargeReadyHash` | Played when charge starts |
| `PlayerAnims_TypeInfo[0x17] + 5` | some idle hash | Played when reverting to idle |

---

## State Flow Diagram (UpdateIn Phase)

```
StateEnter()
    inWaitingForInput = false
    currentStep = 0 (In)
    currentChargeStepDuration = 0.0f
    ↓
BeginDisplayInstructions()
    Shows "Hold A to start charging" text
    ↓
UpdateIn() [per-frame]
    ├─ buttonHeld?
    │    no ─→ not held + inWaiting → return (idle)
    │         not held, animating → reverse anim → if reversed fully: inWaitingForInput=true
    │
    └─ yes ─→ advance intro animation
              progress < 1.0? → return (keep animating)
              progress = 1.0? →
                  currentStep = 1 (Charging)          ← +0x120
                  currentChargeStepDuration = chargeStepDuration  ← +0x128
                  SpawnSunball()                       ← creates sunballProjectileInstance
                  return
    ↓
UpdateCharging() [per-frame, from StateExecute dispatch on step=1]
    [charge loop … auto-release mod operates here]
```

---

## Auto-Start Mod Strategy

### Option A — Prefix on `UpdateIn` (cleanest)

Fire on the first call when `currentStep == 0`, skip the button-hold animation
entirely, and do exactly what the "animation complete" branch does:

```csharp
[HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateIn")]
static class Patch_SunboyShootQTESunballState_UpdateIn
{
    static bool Prefix(SunboyShootQTESunballState __instance)
    {
        unsafe
        {
            byte* b = (byte*)__instance.Pointer;
            int step = *(int*)(b + 0x120);
            if (step != 0) return true;   // not our phase, run original

            // Skip the hold-animation; jump straight to charge start.
            // (UI dismiss and animation are skipped — visually instant.)
            *(int*)(b + 0x120)   = 1;     // currentStep = Charging
            *(float*)(b + 0x128) = *(float*)(b + 0x58);  // currentChargeStepDuration = chargeStepDuration

            // SpawnSunball sets sunballProjectileInstance; required before UpdateCharging runs.
            AccessTools.Method(typeof(SunboyShootQTESunballState), "SpawnSunball")
                       .Invoke(__instance, null);

            return false;   // suppress UpdateIn
        }
    }
}
```

### Option B — Prefix on `BeginDisplayInstructions` + direct SpawnSunball call

Suppress `BeginDisplayInstructions` (already confirmed to be pure-UI, zero
field writes), then on return set the same three fields and call SpawnSunball.
Functionally identical to Option A.

### Why NOT a Postfix on `StateEnter`

`StateEnter` runs before `BeginDisplayInstructions`. A Postfix on `StateEnter`
setting `currentStep = 1` would cause `StateExecute` to dispatch to `UpdateCharging`
before `SpawnSunball` has run → `sunballProjectileInstance == null` → FatalError.
`SpawnSunball` MUST be called in the same patch.

---

## `BeginDisplayInstructions` — Suppress Cleanly

Since `BeginDisplayInstructions` does zero state writes, a simple Prefix returning
`false` silences the text with no side effects:

```csharp
[HarmonyPatch(typeof(SunboyShootQTESunballState), "BeginDisplayInstructions")]
static class Patch_SunboyShootQTESunballState_BeginDisplayInstructions
{
    static bool Prefix() => false;   // suppress "Hold A" instruction text
}
```

---

## Full Automation — Confirmed Complete Picture

With three patches, the entire QTE becomes invisible to the player:

| Patch | Target | Action |
|---|---|---|
| Suppress instructions | Prefix `BeginDisplayInstructions` → `false` | No "Hold A" text |
| Auto-start charge | Prefix `UpdateIn` → set step=1, timer, call SpawnSunball | Skip hold animation |
| Auto-release at peak | Prefix `UpdateCharging` or `StateExecute` → 3 writes | Release at max level |

No button presses, no waiting, no UI visible. The move fires at full power automatically.
