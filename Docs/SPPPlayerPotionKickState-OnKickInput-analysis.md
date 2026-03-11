# Analysis: `SPPPlayerPotionKickState$$OnKickInput`

## Background

`SPPPlayerPotionKickState` is the state machine state active on a **SPP (co-op second) player** during the `PotionKick` combo. It implements `IPotionKickState`, the same interface used by Seraï's `KickPotionState`. `OnKickInput()` is called when that player presses the attack input while in this state.

---

## IL2CPP Parameter Mapping

```c
void SPPPlayerPotionKickState$$OnKickInput(
    longlong param_1,    // this  (SPPPlayerPotionKickState*)
    undefined8 param_2,  // MethodInfo*
    undefined8 param_3,  // IL2CPP extra / unused
    char *param_4        // IL2CPP extra
)
```

---

## Field Offsets Referenced

From `dump.cs` (`SPPPlayerPotionKickState` inherits `StateMachineState<SinglePlayerPlusPlayer>`):

| Offset | C# field | Type |
|---|---|---|
| `+0x58` | `kickWindowDuration` | `float` |
| `+0x5C` | `kickCooldown` | `float` |
| `+0x60` | `kickCallback` | `Action` |
| `+0x68` | `kickEndTime` (private) | `float` |
| `+0x6C` | `nextKickValidTime` (private) | `float` |
| `+0x70` | `kicking` (private) | `bool` |

These match the publicly declared fields exactly.

---

## Logic Walkthrough

### 1. Get current time

```c
fVar3 = UnityEngine.Time.get_time();   // Time.time (seconds since start)
```

A cached `get_time` function pointer (`DAT_183a201c8`) is used, initialised on first call via `FUN_18029a8a0`.

### 2. Set kick window end time

```c
kickEndTime = Time.time + kickWindowDuration;
// param_1 + 0x68 = fVar3 + *(float*)(param_1 + 0x58)
```

This is when the **current kick animation window expires** — `UpdateKicking` polls this to flip `kicking` back to `false`.

### 3. Set next-kick cooldown time

```c
nextKickValidTime = kickEndTime + kickCooldown;
// param_1 + 0x6C = fVar3 + kickCooldown
```

Computed as `kickEndTime + kickCooldown`, i.e., the earliest time a follow-up `OnKickInput` call is allowed. `UpdateKicking` or `StateExecute` guards against re-entry using this value.

### 4. Mark as kicking

```c
kicking = true;   // *(bool*)(param_1 + 0x70) = 1
```

### 5. Invoke the kick callback

```c
lVar2 = kickCallback;
if (lVar2 == 0) return;
(**(code **)(lVar2 + 0x18))(*(undefined8 *)(lVar2 + 0x40), *(undefined8 *)(lVar2 + 0x28));
```

Calls `kickCallback()` via the IL2CPP delegate vtable. Based on `PotionKick` wiring (see `PotionKick.SetAdditionalPlayersState`), `kickCallback` points to `PotionKick.OnKick()`.

`PotionKick.OnKick()` is the method that physically moves the chosen `Projectile` from `kickablePotions` into `kickedPotions` and starts its trajectory toward the enemy.

---

## Equivalent C#

```csharp
public void OnKickInput()
{
    float now = Time.time;
    kickEndTime = now + kickWindowDuration;
    nextKickValidTime = kickEndTime + kickCooldown;
    kicking = true;
    kickCallback?.Invoke();
}
```

---

## Comparison with `KickPotionState.OnKickInput`

`KickPotionState` (Seraï's version) has the same fields at different offsets (it inherits non-generic `StateMachineState`, so the base is smaller). The logic is identical: set timers, set `kicking = true`, invoke the same `kickCallback`. Both states share the same `PotionKick.OnKick` callback.

---

## Key Observations for Auto-Timing

1. **Calling `OnKickInput()` is all that's needed.** It sets timers, marks `kicking = true`, and fires the upstream `PotionKick.OnKick()` chain — the same path as a real button press.

2. **The cooldown guard (`nextKickValidTime`) is set _inside_ this method**, so it won't be checked before the first call. As long as `StateExecute` / `UpdateKicking` respect `nextKickValidTime`, extra calls are naturally rate-limited.

3. **Auto-timing patch target:** Patch `SPPPlayerPotionKickState.StateExecute` or `UpdateKicking`. In the Postfix (or Prefix if `kicking` is already false), detect whether a kickable potion is assigned and simply call `__instance.OnKickInput()`. Because `OnKickInput` is `public`, it can be called directly via interop without unsafe tricks.

4. **Cooldown fields are all `public`**, so a patch could also read `nextKickValidTime` and `kickWindowDuration` directly to decide the right moment, without needing raw pointer arithmetic.

5. **No QTE input scoring here.** `OnKickInput` does not write any `QTEResult` — that happens later in `PotionKick.UpdateKickForPlayer`. The `kickCallback` just sets the potion in motion; the perfect-score credit arrives on the next `UpdateKickForPlayer` frame via the `HasAPlayerKickedPotion` branch.

---

## Related Methods

| Method | Role |
|---|---|
| `SPPPlayerPotionKickState.StateExecute` | Calls `UpdateKicking` each frame; entry point for auto-timing patch |
| `SPPPlayerPotionKickState.UpdateKicking` | Polls `Time.time > kickEndTime` to end kick; enforces `nextKickValidTime` cooldown |
| `SPPPlayerPotionKickState.OnKickInput` | ← **this method** |
| `KickPotionState.OnKickInput` | Identical logic for Seraï's player |
| `PotionKick.OnKick` | Target of `kickCallback`; physically kicks the potion projectile |
| `PotionKick.UpdateKickForPlayer` | Grants `QTEResult.SuccessPerfect` once the potion is kicked |
