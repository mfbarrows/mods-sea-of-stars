# `TimedBlockHandler.GetResult()` — Ghidra Analysis

VA `0x180D9C9E0` — confirmed match from dump.cs.

---

## Field offset map (`param_1` = `this`)

| Ghidra expression | Byte offset | Field | Type |
|---|---|---|---|
| `param_1[10]` | `10 × 8 = 0x50` | `inputResults` | `TeamQTEResult` |
| `*(char *)((longlong)param_1 + 0x3c)` | `0x3C` | `blocking` | `bool` |

---

## Annotated pseudocode

```c
TeamQTEResult GetResult(TimedBlockHandler* self, ...)
{
    // ── Pool management ─────────────────────────────────────────────────
    // Return any previously-held inputResults to pool, then get a fresh one.
    if (self->inputResults != null)
        PoolableClass<TeamQTEResult>.ReturnToPool(self->inputResults);

    self->inputResults = PoolableClass<TeamQTEResult>.GetFromPool();

    // ── Auto-time block check ────────────────────────────────────────────
    // Iterate AutoTimeBlockModifier list from CharacterStatsManager.
    // For each modifier: if (Random.value <= modifier.autoTimeChances) → autoTime = true
    bool autoTime = false;
    var modifiers = CharacterStatsManager.GetModifiersForCharacter<AutoTimeBlockModifier>(self->combatActor);
    foreach (var modifier in modifiers) {
        if (Random.value <= modifier.autoTimeChances) {
            autoTime = true;
            break;
        }
    }
    modifiers.Pool();

    // If auto-time triggered AND not already blocking → call DoBlock() now.
    if (!self->blocking && autoTime)
        DoBlock();

    // ── [Unclear: three nested vtable calls] ─────────────────────────────
    // Three chained virtual calls on `this`. Each returns bool and the
    // chain short-circuits on false. The final selected result
    // (ppppplVar13/pppppplVar14) is passed to FUN_180012710 (likely
    // GetComponent or a combat-actor lookup).
    // These may be: IsInputEnabled() → IsInputPhaseStarted() → something,
    // or a chain selecting the active Rewired.Player for this handler.
    // Not yet determined.

    var actorSource = FUN_180012710(...);
    if (actorSource == null) goto FatalError;

    // ── Grade computation ────────────────────────────────────────────────
    // The entire QTE grade is determined by a SINGLE bool: self->blocking.
    //
    // blocking == true  → EQTEResult = 1 (SuccessBeforeEvent)
    // blocking == false → EQTEResult = 4 (FailDidNoPress)
    //
    uint grade = self->blocking ? 1u : 4u;

    // ── Split: single-player vs SinglePlayerPlus (co-op) ────────────────
    bool isSinglePlayerPlus = *(actorSource + 0xb0) != 0;

    if (!isSinglePlayerPlus) {
        // Single player: get InputManager.FirstPlayer, build QTEResult, add to pool result.
        var inputMgr = InputManager.get_Instance();
        var player   = inputMgr.FirstPlayer;
        QTEResult result = new QTEResult { result = (EQTEResult)grade, owner = player };
        self->inputResults.AddResult(result);
    }
    else {
        // SinglePlayerPlus / co-op: delegate to SinglePlayerPlusManager.
        var sppm = SinglePlayerPlusManager.get_Instance();
        SinglePlayerPlusManager.GetBlockResults(sppm, ref self->inputResults);

        // If auto-time triggered, iterate the collected results and clear
        // the player reference from each entry (zero out owner field).
        // This normalises co-op auto-time results.
        if (autoTime) {
            foreach (QTEResult r in self->inputResults.results)
                r.owner = null;
        }
    }

    return self->inputResults;
}
```

---

## Approximate C# reconstruction

```csharp
// TimedBlockHandler : TimedInputHandler, IUpdatable
public TeamQTEResult GetResult()
{
    // ── Pool management ──────────────────────────────────────────────────
    if (inputResults != null)
        PoolableClass<TeamQTEResult>.ReturnToPool(inputResults);

    inputResults = PoolableClass<TeamQTEResult>.GetFromPool();

    // ── Auto-time block check ────────────────────────────────────────────
    // CharacterStatsManager.GetModifiersForCharacter<AutoTimeBlockModifier>(
    //     combatActor.characterDefinitionId,
    //     includeGlobalModifiers: true,
    //     results: pooledList,
    //     clearList: true)
    bool autoTime = false;
    using PooledList<AutoTimeBlockModifier> modifiers =
        PooledList<AutoTimeBlockModifier>.GetInstanceFromPool();

    CharacterStatsManager.Instance.GetModifiersForCharacter<AutoTimeBlockModifier>(
        (CharacterDefinitionId)combatActor[0x1f],  // combatActor.characterDefinitionId
        includeGlobalModifiers: true,
        modifiers);

    for (int i = 0; i < modifiers.Count; i++)
    {
        AutoTimeBlockModifier mod = modifiers[i];
        if (mod == null) continue;
        if (Random.value <= mod.autoTimeChances)
        {
            autoTime = true;
            break;
        }
    }
    modifiers.Pool();

    // If auto-time AND not already blocking, trigger a block now
    if (!blocking && autoTime)
        DoBlock();

    // ── Determine QTE grade (binary) ─────────────────────────────────────
    // blocking == true  → 1 (SuccessBeforeEvent)
    // blocking == false → 4 (FailDidNoPress)
    EQTEResult grade = blocking ? EQTEResult.SuccessBeforeEvent : EQTEResult.FailDidNoPress;

    // ── Get owning actor via component lookup ────────────────────────────
    // (FUN_180012710 / FUN_180014170 — GetComponent-style lookups)
    // actorSource.0xb0 is a bool flag distinguishing single-player vs SPP.
    var actorSource = GetRelatedActorSource();   // result of chained vtable calls
    if (actorSource == null) throw new NullReferenceException();

    bool isSinglePlayerPlus = actorSource.isSinglePlayerPlus; // *(actorSource + 0xb0)

    if (!isSinglePlayerPlus)
    {
        // Single-player: one result for InputManager.FirstPlayer
        Player player = InputManager.Instance.FirstPlayer;
        inputResults.AddResult(new QTEResult { result = grade, owner = player });
    }
    else
    {
        // Co-op / SinglePlayerPlus: delegate result collection
        SinglePlayerPlusManager.Instance.GetBlockResults(ref inputResults, clearList: false);

        // Auto-time in co-op: clear owner references on all collected results
        if (autoTime)
        {
            List<QTEResult> results = inputResults.results;
            for (int i = results.Count - 1; i >= 0; i--)
            {
                QTEResult r = results[i];
                r.owner = null;         // zero out Rewired.Player ref
                results[i] = r;
            }
        }
    }

    return inputResults;
}
```

---

## Key conclusions

### 1. Block grading is strictly binary

**`SuccessPerfect` is never produced by the block system.** The only two
outcomes are:

| `blocking` at time of `GetResult()` | Grade |
|---|---|
| `true` | `SuccessBeforeEvent` (1) |
| `false` | `FailDidNoPress` (4) |

There are no timing sub-windows for blocks. No `SuccessPerfect`,
`SuccessAfterEvent`, or `FailTooEarly`. The press-timing precision that
matters for attacks simply does not exist for blocks.

### 2. `CanAutoTimeBlock` was inlined into `GetResult()`, not `IUpdatableUpdate()`

The `AutoTimeBlockModifier` random check lives here. This is what was
inlined by IL2CPP — not a separate `CanAutoTimeBlock()` helper called from
the update loop, but this in-place logic inside `GetResult()`. Patching
`AutoTimeBlockModifier.CanAutoTime()` would work here IF a modifier
instance exists; patching `GetInputDown` correctly bypasses all of this.

### 3. The multi-press spam is harmless for grading

Each `DoBlock()` call sets `blocking = true`. Once set, all subsequent
`DoBlock()` calls return immediately (`playingBlockAnimation` guard).
`GetResult()` sees `blocking == true` regardless of how many times
`DoBlock()` was called. The spam causes animation weirdness but does not
affect the grade.

### 4. Current mod status: working correctly

`GetInputDown = true` → `OnInputPressed()` → `DoBlock()` → `blocking = true`
→ `GetResult()` returns `SuccessBeforeEvent`. This is **the best possible
block result**. `SuccessBeforeEvent` is functionally equivalent to
`SuccessPerfect` for blocks; the game does not distinguish them in block
damage reduction.

### 5. Remaining cleanup: multi-press animation spam

`CanPressInput()` (VA `0x180D9D4D0`) re-opens after `DoBlock()` because it
checks something other than `blocking` alone. Understanding it would let us
suppress the repeated `OnInputPressed` calls after the first successful
`DoBlock()`.
