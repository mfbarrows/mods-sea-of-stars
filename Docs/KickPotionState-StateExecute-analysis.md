# Analysis: `KickPotionState$$StateExecute`

## Background

`KickPotionState` is Seraï's (player 1) state machine state during the kick window of the
PotionKick combo. `StateExecute` is called every frame while this state is active. It is the
sole driver of both input detection and animation bookkeeping for the kick.

---

## IL2CPP Parameter Mapping

```c
void KickPotionState$$StateExecute(
    longlong *param_1,       // this (KickPotionState*)
    ...
)
```

---

## Field Offsets Referenced

`param_1` is `longlong*`; index × 8 = byte offset.

| Expression | Byte offset | C# field / type |
|---|---|---|
| `param_1[0xd]` (byte) | `0x68` | `kicking` (bool) |
| `*(float *)((longlong)param_1 + 100)` | `0x64` | `nextKickValidTime` (float) |
| `param_1[0xe]` | `0x70` | `combatActor` (PlayerCombatActor) |
| `*(combatActor + 0x1A8)` | `0x1A8` on `PlayerCombatActor` | `playerInputs` (PlayerInputs : InputCategory) |
| `*(float *)(param_1 + 10)` | `0x50` | `kickWindowDuration` (float) |
| `*(float *)((longlong)param_1 + 0x54)` | `0x54` | `kickCooldown` (float) |
| `*(float *)(param_1 + 0xc)` | `0x60` | `kickEndTime` (float) |
| `*(undefined1 *)(param_1 + 0xd)` | `0x68` | `kicking` (bool) |
| `param_1[0xb]` | `0x58` | `kickCallback` (Action) |

`StringLiteral_1581` = the IL2CPP interned string for the "Attack" button identifier passed to
`InputCategory.GetButtonDown`.

---

## Logic Walkthrough

### Branch 1 — `!kicking` (idle, waiting for input)

```csharp
if (!kicking)
{
    float now = Time.time;

    if (nextKickValidTime <= now)          // cooldown has expired (0 on first frame)
    {
        // Navigate to input handler:
        // combatActor (PlayerCombatActor) @ 0x70
        // .playerInputs (PlayerInputs : InputCategory) @ PCombatActor+0x1A8
        PlayerInputs inputs = combatActor.playerInputs;

        if (inputs.GetButtonDown("Attack"))
        {
            now = Time.time;               // re-read (tiny safety margin)

            kicking             = true;
            kickEndTime         = now + kickWindowDuration;           // @0x60
            nextKickValidTime   = now + kickWindowDuration + kickCooldown; // @0x64

            PlayKickAnim();

            // Invoke kickCallback delegate — this IS PotionKick.OnKick()
            kickCallback?.Invoke();        // param_1[0xb] = @0x58
        }
    }
}
```

`kickCallback` is set up by `PotionKick.DoMove` (before the kick window opens) to point to
`PotionKick.OnKick`. Since `PotionKick.OnKick` is literally just `PotionKick.UpdateKicks()`,
calling it here forces one immediate extra tick of the kick processing loop at the exact moment
the button is pressed.

### Branch 2 — `kicking` (animation playing)

```csharp
else   // kicking == true
{
    float now = Time.time;
    if (kickEndTime <= now)
        kicking = false;    // animation finished, ready for next input
}
```

This is identical in purpose to `UpdateKicking` (which only does the same timer check). Both
`StateExecute` and `UpdateKicking` expire the flag; one of them is likely inlined by the
compiler or called from inside the other.

---

## Equivalent C# (full method)

```csharp
public override void StateExecute()
{
    if (!kicking)
    {
        if (nextKickValidTime <= Time.time)
        {
            var inputs = combatActor.playerInputs;
            if (inputs != null && inputs.GetButtonDown("Attack"))
            {
                float now = Time.time;
                kicking           = true;
                kickEndTime       = now + kickWindowDuration;
                nextKickValidTime = now + kickWindowDuration + kickCooldown;
                PlayKickAnim();
                kickCallback?.Invoke();   // → PotionKick.OnKick() → UpdateKicks()
            }
        }
    }
    else
    {
        if (kickEndTime <= Time.time)
            kicking = false;
    }
}
```

---

## Key Observations

### 1. No proximity check — ever
Neither `StateExecute` nor `UpdateKicking` checks potion position. The kick fires on **any
attack button press** while the cooldown is clear. Whether that produces a QTE result is
entirely determined downstream in `PotionKick.UpdateKickForPlayer` based on potion proximity.

### 2. `OnKickInput` is not called here
`KickPotionState.OnKickInput` is NOT invoked by `StateExecute`. From the logs it also never
fired during real play. It is probably an animation event target (subscribed by `PlayKickAnim`
on the animation clip), triggered mid-animation for sound/VFX rather than for game logic.
This means patching `OnKickInput` will NOT give us a hook on the real kick trigger.

### 3. `kickCallback` is the real hook point
The single call to `kickCallback.Invoke()` is the exact moment the game considers a kick to
have been pressed. An auto-timing patch that invokes `kickCallback` under a controlled
condition (e.g., when `kickablePotions.Count > 0`) would replicate the true button-press path
precisely.

### 4. Cooldown is `kickWindowDuration + kickCooldown`, not just `kickCooldown`
`nextKickValidTime` is set to `Time.time + kickWindowDuration + kickCooldown`. The player
must wait the full animation **plus** the extra cooldown before kicking again. The field names
are slightly misleading — `kickWindowDuration` is actually the kick *animation* duration; the
"window" in the name refers to the window during which `kicking == true`, not the input
acceptance window.

---

## Implications for Auto-Timing

The cleanest approach that avoids "kicking blindly":

1. **Patch `KickPotionState.StateExecute` Prefix** — before the real input check runs, test
   whether any potion is already in `kickablePotions` on the associated `PotionKick` instance.
   If yes, set a flag or directly call `kickCallback.Invoke()`.

2. **Patch `potionReachedSeraiCallback` arrival** — detect the moment a potion lands with
   Seraï (`Projectile.DispatchOnReachedTargetEvent` or similar) and immediately invoke
   `kickCallback` from there rather than waiting for the next `StateExecute` frame.

Option 2 is more precise (fires at exactly the right moment) but requires reading the
`kickCallback` from `KickPotionState` via `PotionKick.seraiKickState` (field at offset `0x110`
on PotionKick). Option 1 is simpler and still fires within one frame of the potion arriving.

---

## Field Summary Table

| C# name | Offset | Role |
|---|---|---|
| `kickWindowDuration` | `0x50` | Duration of kick animation (confusingly named) |
| `kickCooldown` | `0x54` | Extra wait after animation before next input |
| `kickCallback` | `0x58` | `Action` delegate → `PotionKick.OnKick` |
| `kickEndTime` | `0x60` | `Time.time` at which `kicking` is cleared |
| `nextKickValidTime` | `0x64` | `Time.time` at which next input is accepted |
| `kicking` | `0x68` | True while animation is playing |
| `combatActor` | `0x70` | `PlayerCombatActor` (Seraï) |
