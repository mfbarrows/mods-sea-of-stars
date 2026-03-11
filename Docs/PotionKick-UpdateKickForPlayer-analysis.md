# Analysis: `PotionKick$$UpdateKickForPlayer`

## Background

`PotionKick` (internal name for the **Potion Kick** combo move) is a `PlayerCombatMove` involving Seraï and Resh'an. Seraï lobs potions; both players can kick them into enemies. Each frame while `updateKicks == true`, `PotionKick.Update()` calls `UpdateKicks()`, which iterates over each active `SinglePlayerPlusPlayer` and calls `UpdateKickForPlayer(sppPlayer)` for each one.

This analysis covers the private method `UpdateKickForPlayer(SinglePlayerPlusPlayer sppPlayer)`.

---

## IL2CPP Parameter Mapping

The decompiled signature:
```c
void PotionKick$$UpdateKickForPlayer(
    float *param_1,        // this  (PotionKick*)
    ulonglong *param_2,    // sppPlayer (SinglePlayerPlusPlayer*)
    ulonglong *param_3,    // MethodInfo*
    longlong ******param_4 // IL2CPP extra arg
)
```

---

## Field Offsets Referenced

`param_1` is typed as `float*` by Ghidra; multiply index by 4 for byte offset.
`param_2` is typed as `ulonglong*`; multiply index by 8 for byte offset.

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1 + 0x42` → `*(longlong*)` | `0x108` | `PotionKick.serai` (SeraiCombatActor) |
| `param_1 + 0x44` → `*(ulonglong**)` | `0x110` | `PotionKick.seraiKickState` (KickPotionState) |
| `param_1[0x23]` (float) | `0x8C` | `PotionKick.validKickMaxPotionDistance` |
| `param_1 + 0x5e` → `*(undefined8*)` | `0x178` | `PotionKick.kickImpactPosition.x/y` (two floats) |
| `param_1[0x60]` (float) | `0x180` | `PotionKick.kickImpactPosition.z` |
| `param_1 + 0x66` → `*(longlong*)` | `0x198` | `PotionKick.kickablePotions` (List\<Projectile\>) |
| `param_1 + 0x6c` → `*(longlong*)` | `0x1B0` | `PotionKick.qteResultsByPotion` (Dictionary\<Projectile, TeamQTEResult\>) |
| `param_2[0xf]` | `0x78` | `sppPlayer.player` (Rewired.Player) |
| `param_2[0xd]` | `0x68` | `sppPlayer.stateMachine` (StateMachine) |
| `*(ulonglong**)(stateMachine + 0x58)` | varies | current StateMachineState object |

---

## Logic Walkthrough

### 1. Null / validity guards

```c
if (param_2 == null) return;
uVar1 = sppPlayer.player;       // Rewired.Player
if (uVar1 == 0) return;         // player not yet assigned
lVar7 = this.serai;
if (lVar7 == 0) return;         // Seraï combat actor not set
```

### 2. Determine which `IPotionKickState` to use

The method must pick the right kick-state handler depending on **which** player `sppPlayer` represents.

```
InputManager.get_FirstPlayer() → uVar6
```

**Case A — first (local) player (`uVar1 == uVar6`):**

```
potionKickState = this.seraiKickState   // KickPotionState attached to Seraï
```

**Case B — second (SPP) player (`uVar1 != uVar6`):**

```
stateMachine = sppPlayer.stateMachine
currentState = *(stateMachine + 0x58)
// Verify currentState is SPPPlayerPotionKickState (IL2CPP type depth check)
if (type check fails) return;
potionKickState = currentState
```

Both `KickPotionState` and `SPPPlayerPotionKickState` implement `IPotionKickState`:

```csharp
public interface IPotionKickState {
    bool Deflecting { get; }      // slot 0
    void ResetCooldown();         // slot 1
}
```

### 3. Interface cast check

```c
cVar2 = il2cpp_class_is_assignable_from(IPotionKickState, potionKickState)
if (!cVar2) return;   // state doesn't implement IPotionKickState
```

### 4. Backward iteration over `kickablePotions`

```c
count = kickablePotions.Count;
for (int i = count - 1; i >= 0; i--)
{
    Projectile potion = kickablePotions[i];
    TeamQTEResult teamQTE = qteResultsByPotion[potion];   // dictionary lookup
```

Iterates backwards so that removals (done in other methods like `RemoveKickablePotion`) don't invalidate the index in-flight.

### 5. Skip if player already has a result

```c
if (teamQTE.HasSuccessForPlayer(player)) continue;
```

One result per player per potion — once recorded, never overwritten.

### 6. Branch A: Another player has already kicked this potion

```c
if (PotionKick.HasAPlayerKickedPotion(potion))
{
    QTEResult qr = new QTEResult(player, EQTEResult.SuccessPerfect);
    teamQTE.AddResult(qr);   // ← grant automatic perfect to this player
    bVar17 = true;
}
```

If anyone (not necessarily this player) has kicked the potion, the current player receives an automatic **`SuccessPerfect`** result. This is the cooperative hand-off: kicking grants credit to everyone.

### 7. Branch B: Nobody has kicked it yet — proximity check

```c
else
{
    Vector3 potionPosition = potion.transform.position;
    Vector3 delta = potionPosition - this.kickImpactPosition;
    float distance = Vector3.Magnitude(delta);

    if (distance > validKickMaxPotionDistance) continue;   // too far, skip

    QTEResult qr = new QTEResult(player, EQTEResult.SuccessPerfect);
    teamQTE.AddResult(qr);   // ← grant perfect for proximity
    bVar17 = true;
}
```

If no one has kicked the potion yet, but the potion is **within `validKickMaxPotionDistance`** of `kickImpactPosition`, the player also gets `SuccessPerfect`. This handles the case where a player simply stands near the impact zone.

### 8. Post-loop: reset kick cooldown

```c
if (bVar17)
{
    IPotionKickState.ResetCooldown(potionKickState);  // IL2CPP interface slot 1
}
```

If any result was recorded this frame, the kick cooldown is reset to allow follow-up kicks.

---

## Equivalent C#

```csharp
private void UpdateKickForPlayer(SinglePlayerPlusPlayer sppPlayer)
{
    // Guards (offsets: sppPlayer=param_2, serai @ PotionKick+0x108)
    if (sppPlayer == null) return;
    var player = sppPlayer.player;           // sppPlayer+0x78
    if (player == null) return;
    if (serai == null) return;               // PotionKick+0x108

    // Pick the correct IPotionKickState for this player
    // (seraiKickState @ 0x110; SPP path reads current StateMachineState @ stateMachine+0x58)
    IPotionKickState potionKickState;
    if (player == InputManager.FirstPlayer)
    {
        potionKickState = seraiKickState;    // KickPotionState
    }
    else
    {
        var currentState = sppPlayer.stateMachine.CurrentState;  // stateMachine+0x58
        if (currentState is not SPPPlayerPotionKickState sppKickState) return;
        potionKickState = sppKickState;
    }

    if (potionKickState is not IPotionKickState) return;   // interface assignability check

    bool anyResultAdded = false;

    // kickablePotions @ 0x198 — backwards so RemoveKickablePotion mid-loop stays safe
    int count = kickablePotions.Count;
    for (int i = count - 1; i >= 0; i--)
    {
        Projectile potion = kickablePotions[i];

        // qteResultsByPotion @ 0x1B0
        var teamQTE = qteResultsByPotion[potion];

        // One result per (player, potion) — skip if already recorded
        if (teamQTE.HasSuccessForPlayer(player)) continue;

        if (HasAPlayerKickedPotion(potion))
        {
            // Branch A: cooperative auto-grant — someone already kicked this potion,
            // so every other player gets a free SuccessPerfect
            teamQTE.AddResult(new QTEResult(player, EQTEResult.SuccessPerfect));
            anyResultAdded = true;
        }
        else
        {
            // Branch B: proximity check against kickImpactPosition (@ 0x178)
            // validKickMaxPotionDistance @ 0x8C
            Vector3 potionPosition = potion.transform.position;
            Vector3 delta = potionPosition - kickImpactPosition;   // kickImpactPosition @ 0x178
            float distance = Vector3.Magnitude(delta);             // static Magnitude, not .Distance
            if (distance > validKickMaxPotionDistance) continue;   // validKickMaxPotionDistance @ 0x8C

            teamQTE.AddResult(new QTEResult(player, EQTEResult.SuccessPerfect));
            anyResultAdded = true;
        }
    }

    // Reset kick cooldown on the state if any result was recorded this frame
    if (anyResultAdded)
        potionKickState.ResetCooldown();   // IPotionKickState slot 1
}
```

---

## QTEResult Construction Detail

Both branches construct a `QTEResult` struct the same way:

```c
local_58 &= 0xFFFFFFFF_00000000;  // zero lower 32 bits → EQTEResult = 0 = SuccessPerfect
uStack_50 = player;               // Rewired.Player pointer
// Combined 16-byte struct: { result=SuccessPerfect, owner=player }
TeamQTEResult.AddResult(teamQTE, ref struct);
```

The result is **always `EQTEResult.SuccessPerfect`** — there is no partial-success or fail path. The player either gets perfect or gets nothing.

---

## Key Observations for Auto-Timing

1. **`TeamQTEResult.AddResult` is the chokepoint.** Once it fires for a given `(potion, player)` pair, `HasSuccessForPlayer` will return true and subsequent frames skip that pair entirely.

2. **Two independent ways to get credit:**
   - Another player kicked the potion (cooperative auto-grant).
   - The potion is within `validKickMaxPotionDistance` of `kickImpactPosition` (proximity auto-grant).

3. **There is no manual input check in this method.** Input (attack button) is handled upstream: `SPPPlayerPotionKickState.OnKickInput()` → `PotionKick.OnKick()`, which moves the potion from `kickablePotions` into `kickedPotions`. `UpdateKickForPlayer` only reads potion lists and dictionary state — it does not check for button presses.

4. **Auto-timing hook point:** To auto-time the kick for the second player, the simplest approach is to patch `SPPPlayerPotionKickState.StateExecute` or `UpdateKicking` to call `OnKickInput()` automatically when a kickable potion is nearby. Alternatively, since Branch A of this method already grants a perfect result whenever _any_ player has kicked, auto-timing only the first player's kick via `KickPotionState` would cascade free credit to the second player.

5. **`validKickMaxPotionDistance` (0x8C, float)** is the tolerance radius. A larger value means proximity-grant fires more freely and may already grant auto-success without any patches.

---

## Related Classes

| Class | Role |
|---|---|
| `PotionKick` | The `PlayerCombatMove` managing the full combo |
| `KickPotionState` | Seraï's state machine state during kick window; implements `IPotionKickState` |
| `SPPPlayerPotionKickState` | SPP (second) player's equivalent; implements `IPotionKickState` |
| `IPotionKickState` | Interface: `Deflecting`, `ResetCooldown` |
| `TeamQTEResult` | Aggregates `QTEResult` per player; the final hit-data consumer reads `.GetBestResult()` |
| `QTEResult` | Struct: `EQTEResult result + Rewired.Player owner` |
| `SinglePlayerPlusPlayer` | Wrapper for a co-op player controller; holds `player`, `stateMachine` |
