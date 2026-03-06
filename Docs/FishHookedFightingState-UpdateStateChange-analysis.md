# Analysis: `FishHookedFightingState$$UpdateStateChange`

## Overview

This is the virtual method called each frame (from `StateExecute`) on the fish's own state machine
while it is in the **fighting burst** phase — the aggressive side-pulling, high-resistance phase
between calmer idle stretches. It decides when and how to leave `FishHookedFightingState`.

There are three possible exits:
1. Fish is close enough to the catch point → **escalate to `FishHookedFinalFightState`**
2. Jump timer expired and fish can jump → **transition to `FishJumpState`**
3. Fighting burst duration expired → **return to `FishHookedState`** (calm stretch)

---

## Parameter/Register Map

All field offsets are confirmed against dump.cs.

### `param_1` — `this` (`FishHookedFightingState`)

| Ghidra expression | Byte offset | Confirmed field name | Type |
|---|---|---|---|
| `param_1[10]` | 0x50 | *(inherited)* fish owner | `Fish` |
| `*(float *)(param_1 + 0xc)` | 0x60 | `pullStopTime` | `float` |
| `*(float *)(param_1 + 0xd)` | 0x68 | `timeToNextJump` | `float` |
| `*(byte *)((longlong)param_1 + 0x6c)` | 0x6C | `resetJumpTime` | `bool` |

> `param_1` is `longlong*`, so `+0xd` = `+0xd*8` = byte offset 0x68.

### Fish object (`lVar4 = param_1[10]`)

| Ghidra expression | Byte offset | Confirmed field | Type |
|---|---|---|---|
| `*(lVar4 + 0x28)` | 0x28 | `hookedBehaviour` | `FishHookedBehaviour` |
| `*(lVar4 + 0x48)` | 0x48 | `fishData` | `FishingFishData` |
| `*(lVar4 + 0x78)` | 0x78 | `stateMachine` | `StateMachine` |
| `*(lVar4 + 0x140)` | 0x140 | `fishingMinigame` | `FishingMinigame` |
| `*(lVar4 + 0x148)` | 0x148 | `jumpDoneCallback` | `Action` |

### `FishHookedBehaviour` (`*(lVar4 + 0x28)`)

| Byte offset | Confirmed field | Type |
|---|---|---|
| 0x18 | `stamina` | `FishStamina` |
| 0x50 | `lure` | `FishingLure` |

### `FishStamina` (`*(hookedBehaviour + 0x18)`)

| Byte offset | Confirmed field | Type |
|---|---|---|
| 0x34 | `currentStamina` | `float` |

### `FishingFishData` (`*(lVar4 + 0x48)`)

| Byte offset | Confirmed field | Type |
|---|---|---|
| 0x40 | `canJump` | `bool` |
| 0x44 | `minJumpHeight` | `float` |
| 0x48 | `maxJumpHeight` | `float` |
| 0x4C | `jumpUpSpeed` | `float` |

### `FishingMinigame` (`*(lVar4 + 0x140)`)

| Byte offset | Confirmed field | Type |
|---|---|---|
| 0x28 | `finalPullDistance` | `float` |

---

## Annotated Logic (pseudocode)

```csharp
void UpdateStateChange()
{
    // 1. Decrement jump timer each frame
    timeToNextJump -= Time.deltaTime;

    // 2. Guard: need fish, hookedBehaviour, and lure to proceed
    Fish fish = this.owner;                               // param_1[10]
    if (fish == null) goto exception;
    FishHookedBehaviour hb = fish.hookedBehaviour;        // fish + 0x28
    if (hb == null) goto exception;
    FishingLure lure = hb.lure;                           // hookedBehaviour + 0x50
    if (lure == null) goto exception;

    // 3. Check remaining reeling distance
    float distanceLeft = lure.GetTargetZReelingDistanceLeft();

    // 4. Check FishingMinigame catch threshold
    FishingMinigame minigame = fish.fishingMinigame;      // fish + 0x140
    if (minigame == null) goto exception;

    if (distanceLeft <= minigame.finalPullDistance)       // minigame + 0x28
    {
        // --- EXIT PATH A: close enough → escalate to final fight ---
        fish.stateMachine.SetState<FishHookedFinalFightState>();
        return;
    }

    // 5. Check if fighting burst is still active
    if (Time.time <= pullStopTime)                        // param_1 + 0x60
    {
        // Still within the fighting burst window.
        // Check stamina
        FishStamina stamina = hb.stamina;                 // hookedBehaviour + 0x18
        if (stamina == null) goto exception;
        if (stamina.currentStamina <= 0f)                 // stamina + 0x34
            goto exception;                               // no stamina → anomalous exit

        // 6. Check if fish can jump
        FishingFishData data = fish.fishData;             // fish + 0x48
        if (data == null) return;
        if (!data.canJump) return;                        // data + 0x40

        // 7. Jump timer must have expired
        if (timeToNextJump > 0f) return;                  // param_1 + 0x68

        // --- EXIT PATH B: jump ---
        resetJumpTime = true;                             // param_1 + 0x6c

        // Pick a random jump height within fish data range
        float jumpHeight = Random.Range(data.minJumpHeight, data.maxJumpHeight);  // 0x44, 0x48

        // Compute jump duration: time = (2 * height) / upSpeed
        float jumpDuration = (jumpHeight * 2f) / data.jumpUpSpeed;               // 0x4C

        if (!fish.CanJump(jumpDuration)) return;

        // Retrieve and configure FishJumpState
        FishJumpState jumpState = fish.stateMachine.GetState<FishJumpState>();
        if (jumpState == null) return;

        jumpState.field_0x5C = jumpHeight;                // float at JumpState+0x5C
        jumpState.field_0x58 = jumpDuration;              // float at JumpState+0x58
        jumpState.field_0x60 = fish.jumpDoneCallback;     // Action at JumpState+0x60 (fish+0x148)
        // (GC write barrier applied here if IL2CPP incremental GC is active)

        fish.stateMachine.SetState(jumpState, instant: true);
        return;
    }

    // 8. Fighting burst window expired
    // --- EXIT PATH C: return to calm stretch ---
    fish.stateMachine.SetState<FishHookedState>();
}
```

---

## Key Mechanics Explained

### Fighting burst duration (`pullStopTime`)
`pullStopTime` (offset 0x60) is set in `StateEnter` (not visible here). It represents
`Time.time + <burst duration>`. While `Time.time <= pullStopTime`, the fish is in its
aggressive fighting burst. When the burst expires the fish transitions back to `FishHookedState`
(the idle/calm stretch where it isn't pulling hard), and that state will eventually trigger
another fighting burst, creating the alternating rhythm you feel when reeling.

### Jump timer (`timeToNextJump`)
`timeToNextJump` (offset 0x68) counts down from a set value each frame. When it hits 0
(and stamina > 0, canJump = true), the jump fires. The `resetJumpTime` flag (0x6C) signals
`StateEnter` or `StateExecute` to reschedule the next jump timer using `fishData.jumpMinDelay`
/ `fishData.jumpMaxDelay`.

### Final fight threshold (`finalPullDistance`)
`FishingLure.GetTargetZReelingDistanceLeft()` measures how far the fish still needs to be
reeled along the Z axis to reach the catch point. Once that distance falls at or below
`FishingMinigame.finalPullDistance` (field 0x28 = 0.0 in many configs — meaning right at the
edge), the state escalates to `FishHookedFinalFightState`, which uses `GetDirectionToPlayer()`
for side pulls and triggers splash sequences.

### Jump duration formula
```
jumpDuration = (jumpHeight * 2) / jumpUpSpeed
```
This is kinematic: total air time for a symmetric projectile is `2 * peakTime`,
where `peakTime = peakVelocity / g`. The code reinterprets `jumpUpSpeed` as the
effective gravity divisor. `Fish.CanJump(jumpDuration)` likely checks whether the
fish has enough horizontal clearance to complete the arc without hitting a lake boundary.

### Stamina gate on jumps
Jumps are only attempted when `stamina.currentStamina > 0`. When stamina is exhausted
(fish has been fighting hard), the jump path is bypassed entirely. This lets fish that have
been battling for a while become less evasive near the end.

---

## What Is Patchable

| Goal | Target | Method |
|---|---|---|
| Prevent final-fight escalation | `FishingMinigame.finalPullDistance` | Postfix on `FishingMinigame.Awake` or direct field set, make it negative |
| Skip fighting burst entirely | `FishHookedFightingState.UpdateStateChange` | Prefix that returns false and calls `SetState<FishHookedState>` immediately |
| Prevent jumps | `Fish.CanJump` | Postfix → `__result = false` |
| Make jumps always trigger | `timeToNextJump` setter | Force to 0 on `StateEnter` |
| Extend/shorten fighting bursts | `FishHookedFightingState.StateEnter` | Postfix, modify `pullStopTime` field after it's set |
