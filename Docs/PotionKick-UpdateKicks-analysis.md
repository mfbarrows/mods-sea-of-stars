# Analysis: `PotionKick$$UpdateKicks`

## Background

`PotionKick.Update()` calls `UpdateKicks()` each frame while `updateKicks == true`. This method is the top-level per-frame driver: it iterates every active SPP player, delegates per-player kick detection to `UpdateKickForPlayer`, then — once all players are exhausted — runs the three post-processing methods and checks whether the entire kick phase is complete.

---

## IL2CPP Parameter Mapping

```c
void PotionKick$$UpdateKicks(
    longlong ******param_1,    // this  (PotionKick*)
    longlong *****param_2,     // MethodInfo*
    longlong **param_3,        // IL2CPP extra
    longlong ******param_4     // IL2CPP extra
)
```

---

## Field Offsets Referenced

`param_1` is typed as `longlong ******`; each index multiplied by 8 bytes.

| Expression | Byte offset | C# field |
|---|---|---|
| `param_1[0x21]` | `0x108` | `PotionKick.serai` (SeraiCombatActor) |
| `*(int*)(param_1 + 0x11)` | `0x88` | `PotionKick.potionAmountToThrow` (int) |
| `*(int*)(param_1 + 0x1f)` | `0xF8` | `PotionKick.potionThrowDoneCount` (int) |
| `param_1[0x33]` | `0x198` | `PotionKick.kickablePotions` (List\<Projectile\>) |
| `*(byte*)((longlong)param_1 + 0x193)` | `0x193` | `PotionKick.updateKicks` (bool) |

`serai[0x2a]` = byte offset `0x150` from `PlayerCombatActor` = `liveManaHandler` (PlayerActorLiveManaHandler, inherited by SeraiCombatActor).  
The subsequent `[10][0xf]` chain navigates to a `PlayerCombatActorDependencies` object.  
`dependencies[0xe]` = byte `0x70` = `boostController` (CombatBoostLevelController).

---

## Logic Walkthrough

### 1. Singleton access / lazy init

The method opens by using the standard IL2CPP cached-singleton pattern to resolve `SinglePlayerPlusManager.Instance`. This pattern:
- Reads the `Il2CppClass*` via `Method$Manager<SinglePlayerPlusManager>.get_Instance()`.
- Checks the `0x132` initialized flag; if unset, calls `il2cpp_runtime_class_init`.
- Dereferences into the static field blob to retrieve the singleton pointer.

Because the list is re-fetched from the singleton inside the loop (see below), this pattern appears **twice per iteration**.

### 2. Main loop — iterate SPP players

```c
int i = 0;
while (true)
{
    List<SinglePlayerPlusPlayer> players = SinglePlayerPlusManager.Instance.ActiveAdditionalPlayers;
    // (exact field: some List<SPP Player> exposed by the manager)

    if (players == null) goto NullRefHandler;
    if (i >= players.Count)               // ← all players processed → go to post-processing
    {
        // ... post-processing block (Step 3) ...
        break;
    }

    // Re-fetch for the actual item access (compiler bounds-check hygiene)
    players = SinglePlayerPlusManager.Instance.ActiveAdditionalPlayers;
    if (players == null) goto NullRefHandler;

    T[] items = players._items;           // backing array
    if (items == null) goto NullRefHandler;
    if (i >= items.Length)
        ThrowArgumentOutOfRange();        // shouldn't happen; length ≥ count

    SinglePlayerPlusPlayer player = items[i];
    UpdateKickForPlayer(player);
    i++;
}
goto NullRefHandler;                      // FUN_1802845b0 — NullRef crash handler
```

The `while(true)` with an explicit counter is Ghidra's view of a `for` loop. The list is re-fetched from the singleton at the top of every iteration — the compiler avoids caching it across unknown calls.

### 3. Post-processing block (entered when `i >= players.Count`)

Runs three batch methods that operate on all potions at once:

```c
ProcessSuccessfulKicks();         // award hits, trigger FX for potions kicked by all players
ProcessOutdatedPotions();         // remove any potions that passed their lifetime without being kicked
ProcessPotionsKickedByAllPlayers(); // finalize potions where every player got a QTE result
```

### 4. Termination check

```c
if (kickablePotions != null)
{
    if (kickablePotions.Count > 0 || potionThrowDoneCount < potionAmountToThrow)
        return;   // still more potions to go — keep updating next frame

    updateKicks = false;   // ← flip the Update gate off; no more per-frame iterations

    // Remove Seraï's boost charge FX if one is active
    var deps = serai.liveManaHandler[...] as PlayerCombatActorDependencies;
    if (deps?.boostController != null)
        CombatBoostLevelController.RemoveBoostFX(deps.boostController);
}
```

`potionThrowDoneCount < potionAmountToThrow` means Resh'an hasn't finished throwing all potions yet — if true, we wait even if the kick queue is empty (another potion might be incoming).

Setting `updateKicks = false` stops `PotionKick.Update()` from calling `UpdateKicks()` on future frames. `RemoveBoostFX` clears the charge-level visual on Seraï once the entire combo is resolved.

---

## Equivalent C#

```csharp
private void UpdateKicks()
{
    var manager = SinglePlayerPlusManager.Instance;
    var players = manager.activeAdditionalPlayers; // (exact field TBD at runtime)

    for (int i = 0; i < players.Count; i++)
    {
        UpdateKickForPlayer(players[i]);
    }

    ProcessSuccessfulKicks();
    ProcessOutdatedPotions();
    ProcessPotionsKickedByAllPlayers();

    if (kickablePotions == null) return;

    if (kickablePotions.Count > 0 || potionThrowDoneCount < potionAmountToThrow)
        return;

    updateKicks = false;

    var deps = serai?.liveManaHandler?.[...]?.GetComponent<PlayerCombatActorDependencies>();
    if (deps?.boostController != null)
        deps.boostController.RemoveBoostFX();
}
```

---

## Key Observations for Auto-Timing

1. **`updateKicks` is the gate.** `PotionKick.Update()` only calls `UpdateKicks()` while this flag is `true`. It gets set to `false` here once the entire combo resolves. Any auto-timing patch inside `UpdateKicks` or its callees will only run during the active kick window.

2. **The per-player dispatch is a direct iteration.** Each SPP player gets exactly one `UpdateKickForPlayer` call per frame. There is no early-out between players — all players are processed regardless of what the previous one did.

3. **`ProcessSuccessfulKicks`, `ProcessOutdatedPotions`, `ProcessPotionsKickedByAllPlayers` are called every frame** (after the player iteration), not just on the last frame. These are idempotent batch processors; calling them more often than needed is safe.

4. **Termination precondition:** Both `kickablePotions.Count == 0` AND `potionThrowDoneCount >= potionAmountToThrow` must be true before `updateKicks` is cleared. Auto-timing all kicks immediately (bypassing the throw animation) could cause the second condition to resolve first, which is fine — the check is an AND anyway.

5. **`RemoveBoostFX` is the visual cleanup.** If auto-timing causes the combo to complete earlier than normal, the boost charge visual gets cleared by this method regardless. No extra cleanup is needed in the mod.

---

## Related Classes and Methods

| Method / Class | Role |
|---|---|
| `PotionKick.Update()` | Calls `UpdateKicks()` each frame while `updateKicks == true` |
| `PotionKick.UpdateKickForPlayer(SPPPlayer)` | Per-player input detection + QTE result recording |
| `PotionKick.ProcessSuccessfulKicks()` | Applies hit consequences for all accepted kicks |
| `PotionKick.ProcessOutdatedPotions()` | Cleans up expired/missed potions |
| `PotionKick.ProcessPotionsKickedByAllPlayers()` | Finalises potions where all active players recorded a result |
| `SinglePlayerPlusManager` | Singleton holding the `List<SinglePlayerPlusPlayer>` iterated here |
| `CombatBoostLevelController.RemoveBoostFX` | Clears Seraï's charge-level visual at combo end |
