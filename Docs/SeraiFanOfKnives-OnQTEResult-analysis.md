# `SeraiFanOfKnives$$OnQTEResult` — Decompiled Code Analysis

## Signature

```csharp
private void OnQTEResult(TeamQTEResult qteResults)
// param_1 = this   (SeraiFanOfKnives*)
// param_2 = qteResults  (TeamQTEResult managed object reference)
```

---

## Field offset map (from dump.cs)

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `param_1[0x20]` | `0x100` | `QTEResult qteResult` (bytes 0–7) |
| `param_1[0x21]` | `0x108` | `QTEResult qteResult` (bytes 8–15) — `QTEResult` is a 16-byte value type |
| `*(byte*)(param_1 + 0x131)` | `0x131` | `bool qteResultPending` |
| `(char)param_1[0x26]` | `0x130` | `bool waitingForQTE` |

---

## Step-by-step translation

```c
if (param_2 == 0)                         // null-check on qteResults
    throw NullReferenceException;         // FUN_1802845b0 = IL2CPP NRE helper

// TeamQTEResult.GetBestResult() → stack-allocated local copy
plVar5 = TeamQTEResult$$GetBestResult(local_18, param_2, ...);

// this.qteResult = local_18  (16-byte struct copy)
param_1[0x20] = *plVar5;    // qteResult bytes 0–7
param_1[0x21] = plVar5[1];  // qteResult bytes 8–15

// Incremental GC write barrier for the reference inside qteResult
if (DAT_183a0b2e4 != 0) { ... }   // standard IL2CPP barrier pattern

// this.qteResultPending = false
*(byte*)((char*)param_1 + 0x131) = 0;

// if (!this.waitingForQTE) return;
if ((char)param_1[0x26] == '\0') return;

// this.waitingForQTE was true → advance the skill
SeraiFanOfKnives$$ExitNextPortal(param_1, ...);
```

---

## Reconstructed C#

```csharp
private void OnQTEResult(TeamQTEResult qteResults)
{
    // qteResults null-check is implicit in IL2CPP (NRE path)

    // Flatten the per-player results down to the single best outcome
    // and store it for use by Update / TryApplyFinalHit.
    qteResult = TeamQTEResult.GetBestResult(qteResults);

    // The pending flag was set by OnThrowProjectile right before it
    // subscribed this callback; clear it now that we have the answer.
    qteResultPending = false;

    // Only advance if Update put us into the "waiting for QTE answer"
    // state.  If waitingForQTE is false the result arrived too late
    // (or the move is already winding down) and should be ignored.
    if (!waitingForQTE)
        return;

    ExitNextPortal();
}
```

---

## Behavioural notes

### `waitingForQTE` is the gate
`ExitNextPortal()` is only reached when `waitingForQTE == true`.  
`Update()` / `UpdateThrowing()` sets this flag each time a knife is thrown and a QTE window is opened.  
Once the result arrives, the flag is **not** cleared here — that is done inside `ExitNextPortal()` (or the method it calls).

### Why auto-timing caused an infinite loop
The normal flow is one-shot per throw:

```
OnThrowProjectile
  → waitingForQTE = true
  → arm TimedAttackHandler QTE window

[player presses button or window expires]
  → OnQTEResult fires
      → qteResultPending = false
      → waitingForQTE == true → ExitNextPortal()
          → Serai exits portal, starts next arc
          → if more throws remain: waitingForQTE = true again
```

With `CanAutoTimeHit` forced to `true` on every call, `TimedAttackHandler.GetResult` immediately fires `OnAttackResultReady → SuccessPerfect` for **every individual QTE window**, including every re-arm inside `ExitNextPortal`.  Because there is no cooldown between "result delivered" and "next window armed", the whole chain ran synchronously/re-entrantly at full CPU speed, repeating until Unity's call stack limit was hit or the scene became inconsistent.

### Fix applied (`AutoTimeAttackPatches.cs`)
`Patch_CanAutoTimeHit.Postfix` now guards on `moveDefinition.name.Contains("FanOfKnives")`:

```csharp
static void Postfix(PlayerCombatMoveDefinition moveDefinition, ref bool __result)
{
    if (moveDefinition != null && moveDefinition.name.Contains("FanOfKnives"))
        return;   // leave __result as-is; player must time manually
    __result = true;
}
```

This preserves the player-skill loop behaviour while still auto-timing all other attacks.

---

## QTEResult struct layout (inferred)

`QTEResult` occupies exactly 16 bytes (`param_1[0x20]` and `param_1[0x21]`).  
It is a value type (struct), not a reference — the GC write barrier fires on the reference(s) embedded *inside* it (the `TeamQTEResult` backing object), not on `qteResult` itself.
