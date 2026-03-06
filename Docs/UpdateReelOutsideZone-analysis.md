# Analysis: `FishermanReelFishState$$UpdateReelOutsideZone`

## Overview

`UpdateReelOutsideZone` is called every frame by `StateExecute` while the player is actively
reeling **and** the lure/fish is outside the reel sweet-spot zone. It applies three simultaneous
penalties and coordinates haptic/visual feedback:

1. **Rumble** the controller
2. **Reduce `lineMaxHP`** (the ceiling for line health regeneration)
3. **Drain `lineHPLeft`** and trigger `LoseFish()` if it hits zero
4. **Build up** the camera-shake feedback value (`FishReelingFeedbackHandler.shakeValue`)
5. **Advance `hookedBehaviour.targetZ`** — pushing the fish's pull target away at the outside-shape release speed

---

## Confirmed Field Map

All offsets verified against `Dump/dump.cs`.

### `param_1` — `this` (`FishermanReelFishState`)

> `param_1` is typed `char**`, so `param_1[N]` = byte offset `N × 8`.
> Byte-exact accesses use explicit `(longlong)` casts.

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `param_1[10]` | `0x50` | `fisherman` (inherited from `FishermanState`) | `FishermanController` |
| `param_1[0xd]` | `0x68` | `reelOutsideZoneVibration` | `VibrationData` |
| `param_1[0xf]` | `0x78` | `lure` | `FishingLure` |
| `*(float *)(param_1 + 0x11)` | `0x88` | `lineHPLeft` | `float` |
| `*(float *)((longlong)param_1 + 0x8c)` | `0x8C` | `lineMaxHP` | `float` |

### `FishermanController` (`param_1[10]`, state+0x50)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(param_1[10] + 0x68)` | `0x68` | `reelingOutsideShapeReleaseSpeed` | `float` |
| `*(param_1[10] + 0x74)` | `0x74` | `timeToMaxReelingFeedback` | `float` |
| `*(param_1[10] + 0xa0)` | `0xA0` | `fishingPole` | `FishingPole` |
| `*(param_1[10] + 0xa8)` | `0xA8` | `fishingMinigame` | `FishingMinigame` |
| `*(param_1[10] + 0xd8)` | `0xD8` | `player` | `Player` |

### `VibrationData` (`reelOutsideZoneVibration`, state+0x68)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(reelOutsideZoneVibration + 0x18)` | `0x18` | `vibrationStrength` | `float [0,1]` |
| `*(reelOutsideZoneVibration + 0x1c)` | `0x1C` | `duration` | `float` |

### `FishingPole` (`fisherman.fishingPole`, fisherman+0xA0)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(fishingPole + 0x38)` | `0x38` | `poleData` | `FishingPoleData` |

### `FishingPoleData` (`fishingPole.poleData`, fishingPole+0x38)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(poleData + 0x38)` | `0x38` | `fishingLineMaxHPReductionSpeed` | `float [0,1]` |

### `FishingMinigame` (`fisherman.fishingMinigame`, fisherman+0xA8)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(fishingMinigame + 0x78)` | `0x78` | `reelingFeedbackHandler` | `FishReelingFeedbackHandler` |

### `FishReelingFeedbackHandler` (`fishingMinigame.reelingFeedbackHandler`, fishingMinigame+0x78)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(feedbackHandler + 0x24)` | `0x24` | `shakeValue` | `float` |
| `*(feedbackHandler + 0x28)` | `0x28` | `increasing` | `bool` |

### `FishingLure` (`lure`, state+0x78)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(lure + 0xb8)` | `0xB8` | `hookedFish` | `Fish` |

### `Fish` (`lure.hookedFish`, lure+0xB8)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(hookedFish + 0x28)` | `0x28` | `hookedBehaviour` | `FishHookedBehaviour` |

### `FishHookedBehaviour` (`hookedFish.hookedBehaviour`, hookedFish+0x28)

| Ghidra expression | Byte offset | `dump.cs` field | Type |
|---|---|---|---|
| `*(hookedBehaviour + 0x5c)` | `0x5C` | `targetZ` | `float` (protected) |
| vtable+`0x178` | — | `SetTargetZ(float)` | virtual method (Slot 4, adjusted for vtable base) |

---

## Annotated Logic (pseudocode)

```csharp
void UpdateReelOutsideZone()
{
    // ── 0. Manager singleton init (one-time) ──────────────────────────────────
    // DAT_183a0ff01 is a static bool flag (already-initialized guard).
    // If not yet initialised, resolve InputManager.get_Instance() and cache it.

    // ── 1. Null guards ────────────────────────────────────────────────────────
    InputManager inputManager = Manager<InputManager>.get_Instance();
    FishermanController fisherman = this.fisherman;          // state + 0x50
    VibrationData vib = this.reelOutsideZoneVibration;      // state + 0x68
    FishingLure lure = this.lure;                            // state + 0x78

    if (fisherman == null || inputManager == null || vib == null)
        goto NullError;

    // ── 2. Rumble ─────────────────────────────────────────────────────────────
    Player player = fisherman.player;                        // fisherman + 0xD8
    InputManager.RumbleController(inputManager, player, vib.vibrationStrength);

    // ── 3. lineMaxHP decay ────────────────────────────────────────────────────
    // While outside the zone the line's regeneration ceiling is compressed.
    FishingPole pole = fisherman.fishingPole;                // fisherman + 0xA0
    if (pole != null)
    {
        FishingPoleData poleData = pole.poleData;            // pole + 0x38
        if (poleData != null)
        {
            float reductionSpeed = poleData.fishingLineMaxHPReductionSpeed; // poleData + 0x38
            this.lineMaxHP -= Time.deltaTime * reductionSpeed; // state + 0x8C
        }
    }

    // ── 4. lineHPLeft drain ───────────────────────────────────────────────────
    // Raw drain: 1 HP/sec (deltaTime), independent of any pole stat.
    float previousHP = this.lineHPLeft;                      // state + 0x88
    this.SetLineHP(previousHP - Time.deltaTime);

    if (this.lineHPLeft <= 0f)
    {
        this.LoseFish();
        return;
    }

    // ── 5. Camera-shake build-up ──────────────────────────────────────────────
    FishingMinigame minigame = fisherman.fishingMinigame;    // fisherman + 0xA8
    if (minigame != null)
    {
        FishReelingFeedbackHandler handler = minigame.reelingFeedbackHandler; // minigame + 0x78
        if (handler != null)
        {
            // Normalise delta by the time it takes to reach maximum shake.
            float increment = Time.deltaTime / fisherman.timeToMaxReelingFeedback; // fisherman + 0x74
            float newShake = Mathf.Min(handler.shakeValue + increment, 1f);
            handler.shakeValue  = newShake;                  // feedbackHandler + 0x24
            handler.increasing  = true;                      // feedbackHandler + 0x28

            // ── 6. targetZ release ────────────────────────────────────────────
            // Move the fish's reeling target away from the catch point at
            // reelingOutsideShapeReleaseSpeed per second, so that a player
            // who stops reeling or reels badly loses ground.
            Fish hookedFish = lure.hookedFish;               // lure + 0xB8
            if (hookedFish != null)
            {
                FishHookedBehaviour hb = hookedFish.hookedBehaviour; // fish + 0x28
                if (hb != null)
                {
                    float outsideSpeed = fisherman.reelingOutsideShapeReleaseSpeed; // fisherman + 0x68
                    float newTargetZ   = hb.targetZ + Time.deltaTime * outsideSpeed; // hb + 0x5C
                    hb.SetTargetZ(newTargetZ);               // vtable + 0x178
                    return;
                }
            }
        }
    }

    // ── Fallthrough null-error path ───────────────────────────────────────────
    // IL2CPP NullReferenceException path (FUN_1802845b0 → swi(3))
    NullError:
    throw new NullReferenceException();
}
```

---

## Key Mechanics Explained

### `lineMaxHP` compression (`state + 0x8C`)
Each frame outside the zone, `lineMaxHP` shrinks by
`deltaTime × FishingPoleData.fishingLineMaxHPReductionSpeed` (range 0–1).
Because `SetLineHP` clamps line health to the current max, this acts as a
**soft pressure ceiling**: line health can no longer regenerate as high as before,
making recovery progressively harder the longer the player stays outside the zone.
The inside-zone method (`UpdateReelInsideZone`) is expected to restore it.

### `lineHPLeft` direct drain (`state + 0x88`)
Separate from the ceiling, the raw health drains at **1 HP per second** (`deltaTime`)
regardless of pole stats. Hitting zero immediately calls `LoseFish()`.

### Camera shake ramp (`FishReelingFeedbackHandler.shakeValue`)
`shakeValue` increments by `deltaTime / timeToMaxReelingFeedback` per frame,
clamped to 1.0. Setting `increasing = true` tells `FishReelingFeedbackHandler.Update`
to apply the shake rather than decrement with `decrementSpeed`. This gives a
progressively more intense rumble/shake the longer the player is outside the zone.

### Fish target-Z release (`FishHookedBehaviour.targetZ`)
`targetZ` is the Z position the fish is being pulled/reeled toward the catch point.
Each frame outside the zone it is incremented by
`deltaTime × fisherman.reelingOutsideShapeReleaseSpeed`, pushing it _away_ from the
catch point. The player loses the "progress" they made reeling the fish in.

### Static method-pointer cache (`DAT_183a201d8`)
`Time.get_deltaTime()` is resolved once via `FUN_18029a8a0` (IL2CPP runtime lookup)
and cached in the global field `DAT_183a201d8`. Subsequent calls skip the lookup.
The three deltaTime reads in the function all use this same cache.

---

## Patchable Goals

| Modding goal | Target | Approach |
|---|---|---|
| No line HP loss outside zone | `FishermanReelFishState.UpdateReelOutsideZone` | Prefix → `return false` (skip entirely) |
| Slower lineMaxHP compression | `FishingPoleData.fishingLineMaxHPReductionSpeed` | Postfix on `FishingPoleData.ctor` / direct field write |
| Prevent fish target-Z rollback | `FishHookedBehaviour.SetTargetZ` | Prefix → clamp new value to `≤ current targetZ` |
| Disable controller rumble | `InputManager.RumbleController` | Prefix → `return false` |
| Faster/slower shake build-up | `FishermanController.timeToMaxReelingFeedback` | Postfix on `FishermanController.ctor` / direct field write |
