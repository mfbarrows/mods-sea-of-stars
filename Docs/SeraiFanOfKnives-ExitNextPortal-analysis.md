# `SeraiFanOfKnives$$ExitNextPortal` — Decompiled Code Analysis

## Signature

```csharp
private void ExitNextPortal()
// param_1 = this  (SeraiFanOfKnives*)
```

---

## Field offset map

All indexing uses `longlong*` arithmetic: `param_1[N]` = byte offset `N × 8`.

| Assembly expression | Byte offset | C# field |
|---|---|---|
| `*(uint*)(param_1 + 0x20)` | `0x100` | `QTEResult qteResult` → first field `EQTEResult result` (4 bytes @ struct+0x0) |
| `(int)param_1[0x1a]` | `0xD0` | `int freeJumps` |
| `(int)param_1[0x1f]` | `0xF8` | `int hitCount` |
| `*(byte*)(param_1 + 0x26)` | `0x130` | `bool waitingForQTE` |
| `param_1[0x27]` | `0x138` | `SeraiCombatActor seraiActor` |
| `param_1[0x24]` | `0x120` | `FX_Portal firstPortalInstance` |
| `param_1[0x25]` | `0x128` | `FX_Portal secondPortalInstance` |
| `param_1[0x2a]` | `0x150` | `FX_Portal currentPortalInstance` |
| `param_1[0xf]` | `0x78` | inherited base-class reference (owner `CombatActor`, base of `PlayerCombatMove`) |

---

## `EQTEResult` enum values (for condition decoding)

```csharp
SuccessPerfect     = 0
SuccessBeforeEvent = 1
SuccessAfterEvent  = 2
FailTooEarly       = 3
FailDidNoPress     = 4
NoQTE              = 5
```

---

## Main branch condition decoded

```c
if (((1 < *(uint*)(param_1 + 0x20)) && (*(uint*)(param_1 + 0x20) != 2))
    && ((int)param_1[0x1a] <= (int)param_1[0x1f]))
```

Translates to:

```csharp
bool qteFailed = qteResult.result > (EQTEResult)1 && qteResult.result != EQTEResult.SuccessAfterEvent;
//  result ∈ { FailTooEarly(3), FailDidNoPress(4), NoQTE(5) }

bool usedUpFreeJumps = freeJumps <= hitCount;

if (qteFailed && usedUpFreeJumps)
    // → WIND-DOWN PATH
else
    // → CONTINUE PATH
```

**Semantic interpretation:**  
`freeJumps` (inspector-set) is the minimum number of throws guaranteed regardless of QTE result.  
Once `hitCount ≥ freeJumps`, a failed QTE ends the move.  
A successful QTE (result ≤ 2 / `SuccessAfterEvent`) always continues, regardless of `hitCount`.

---

## Step-by-step translation

### Preamble (IL2CPP class-init check)

```c
if (DAT_183a0fb0a == '\0') {
    FUN_1802a4cc0(&UnityEngine.Object_TypeInfo);  // ensure UnityEngine.Object class is initialised
    DAT_183a0fb0a = '\x01';
}
```

Standard IL2CPP lazy class-init guard — runs at most once per class.

### Always: clear `waitingForQTE`

```csharp
waitingForQTE = false;   // *(byte*)(param_1 + 0x26) = 0
```

Remove the "waiting for QTE result" flag regardless of which path follows.

---

### WIND-DOWN PATH (QTE failed AND free jumps exhausted)

```csharp
// Deactivate seraiActor's GameObject
seraiActor.gameObject.SetActive(false);

// Get the coroutine host (base-class owner CombatActor → component at vtable slot 0x2d)
var host = ((BaseOwner)param_1[0xf]).GetComponent_at_0x168();

// Allocate and configure WaitAndExitFirstPortalCoroutine state machine
var coroutine = new SeraiFanOfKnives.<WaitAndExitFirstPortalCoroutine>d__49();
coroutine.__4__this = this;   // capture `this`
coroutine.__state  = 0;       // iterator initial state

// GC write barrier on the captured `this` reference
// ... (LOCK/UNLOCK CAS loop on incremental GC card table)

// Start the coroutine on this MonoBehaviour
this.StartCoroutine(coroutine);
```

**Purpose:** Serai is hidden (SetActive false) and the game waits for the portal to finish before completing the move — a graceful wind-down when no further throws are warranted.

---

### CONTINUE PATH (QTE succeeded or still has free jumps)

#### Portal alternation logic

```csharp
FX_Portal lVar5 = currentPortalInstance;  // 0x150
FX_Portal lVar3 = firstPortalInstance;    // 0x120

FX_Portal nextPortal;
if (lVar5 == null && lVar3 == null)          goto ErrorPath;   // NRE
if (lVar5 == null)   nextPortal = lVar3;     // null vs ref → use first
else if (lVar3 == null) nextPortal = lVar5;  // ref vs null → use current
else if (lVar5 == lVar3) nextPortal = secondPortalInstance;  // same → switch to second
else                     nextPortal = firstPortalInstance;   // different → switch to first
```

The three `UnityEngine.Object_TypeInfo` checks + `IntPtr_TypeInfo` comparison are how IL2CPP implements a null-aware `object.ReferenceEquals` for a managed type vs. `null`/`IntPtr.Zero`.  In plain C# this is just:

```csharp
FX_Portal nextPortal = (currentPortalInstance == firstPortalInstance)
    ? secondPortalInstance
    : firstPortalInstance;
```

Serai always alternates between the two portals on each throw cycle.

#### Conclude and jump

```csharp
SetCurrentPortalInstance(nextPortal);
JumpOutOfPortal();
```

`SetCurrentPortalInstance` records which portal Serai is exiting from, then `JumpOutOfPortal` launches the jump animation/physics for the next throw arc.

---

## Reconstructed C#

```csharp
private void ExitNextPortal()
{
    waitingForQTE = false;

    bool qteFailed = qteResult.result > EQTEResult.SuccessBeforeEvent
                     && qteResult.result != EQTEResult.SuccessAfterEvent;
    // ≡ result ∈ { FailTooEarly, FailDidNoPress, NoQTE }

    if (qteFailed && freeJumps <= hitCount)
    {
        // Move is ending: hide Serai and wait for the portal sequence to close.
        seraiActor.gameObject.SetActive(false);
        StartCoroutine(WaitAndExitFirstPortalCoroutine());
        return;
    }

    // Alternate between the two portals on each throw.
    FX_Portal nextPortal = (currentPortalInstance == firstPortalInstance)
        ? secondPortalInstance
        : firstPortalInstance;

    SetCurrentPortalInstance(nextPortal);
    JumpOutOfPortal();
}
```

---

## Connection to the infinite-loop bug

With `CanAutoTimeHit` forced to `true`, every QTE window was resolved as `SuccessPerfect` (value `0`).  
The `qteFailed` condition is therefore always **false**, so the wind-down branch is never entered.  
`freeJumps <= hitCount` is irrelevant because `qteFailed` short-circuits the `&&`.  
`ExitNextPortal` always falls through to `JumpOutOfPortal → OnThrowProjectile → new QTE window → OnQTEResult → ExitNextPortal → …` at CPU speed.

The fix in `AutoTimeAttackPatches.cs` lets the QTE result be whatever the player (or default failure path) produces for FanOfKnives, so `qteFailed` can eventually become `true` and kick the move into its wind-down sequence.
