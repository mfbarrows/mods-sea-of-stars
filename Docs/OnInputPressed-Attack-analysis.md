# `AbstractTimedAttackHandler.OnInputPressed()` — Ghidra Analysis

RVA `0xD99AD0` / VA `0x180D99AD0` — confirmed match from dump.cs.

---

## Field offset map (`param_1` = `this`)

Inherits from `TimedInputHandler` (base fields) + own fields:

| Ghidra expression | Byte offset | Field | Type |
|---|---|---|---|
| `*(float*)(param_1 + 0x28)` | `0x28` | `windowDurationMultiplier` *(base)* | `float` |
| `param_1[0x39]` | `0x39` | `inputPressed` | `bool` |
| `*(float*)(param_1 + 0x40)` | `0x40` | `beforeHitWindowEndTime` | `float` |

`FUN_1800127d0` — same lookup helper seen in `DoBlock()`; retrieves a linked
component or actor reference.
`*(lVar3 + 0xe0)` — a reference on that component, likely the current attack
move definition or animation event object.
`*(float*)(pcVar4 + 0x44)` — a float on that object: the
raw `BeforeQTEOkWindow` duration for this specific move.

---

## Annotated pseudocode

```c
void OnInputPressed(AbstractTimedAttackHandler* self, ...)
{
    // Resolve and cache Time::get_time (IL2CPP static lookup)
    float now = Time.time;

    // Get the current move/animation-event object
    var moveSource = GetRelatedComponent(self, ...);
    if (moveSource == null || *(moveSource + 0xe0) == null)
        FatalError();

    // Read the per-move BeforeQTEOkWindow duration
    float rawBeforeWindow = *(float*)(*(moveSource + 0xe0) + 0x44);

    // Record the press
    self->inputPressed = true;                                          // 0x39

    // Store the deadline: how long after NOW the "before-event OK" window stays open.
    // windowDurationMultiplier scales the window (e.g. accessibility relic widens it).
    self->beforeHitWindowEndTime =
        rawBeforeWindow * self->windowDurationMultiplier + now;         // 0x40
}
```

---

## Key conclusions

### 1. `OnInputPressed` does NOT assign a QTE grade

It only records two things:
- `inputPressed = true` — the press happened
- `beforeHitWindowEndTime` — the deadline up to which this press can still
  count as `SuccessBeforeEvent`

The grade (`SuccessPerfect`, `SuccessBeforeEvent`, `SuccessAfterEvent`,
`FailTooEarly`) is computed **later**, almost certainly in
`IUpdatableUpdate()` when the attack animation event actually fires.
At that moment the system compares `Time.time` against the stored window
timestamps to decide which bucket the press falls into.

### 2. The timing model for attacks (reconstructed)

```
Press happens     Attack event fires (IUpdatableUpdate polls)
     │                          │
     ▼                          ▼
inputPressed = true     check stored window deadlines:
beforeHitWindowEndTime set   now < beforeHitWindowEndTime  → SuccessBeforeEvent
                             now ≈ eventTime ± perfectWindow → SuccessPerfect
                             now < afterHitWindowEndTime   → SuccessAfterEvent
                             (no press at all)             → FailDidNoPress
```

`afterHitWindowEndTime` (offset `0x3C`) is set elsewhere — likely in
`BeginInputPhase` or a separate phase-tracking method.

### 3. Why `CanAutoTimeHit` produces `SuccessPerfect`

The auto-time branch in `IUpdatableUpdate()` bypasses `OnInputPressed()`
entirely. Instead of going through the "record press → wait for event → grade"
pipeline, it fires the result directly as `SuccessPerfect` at exactly the
right moment. Our patch (`CanAutoTimeHit → true`) plugs into this path
cleanly.

### 4. Implication for the block side

The block equivalent is `TimedBlockHandler.OnInputPressed()` (RVA `0xD9D290`).
Based on this pattern, it likely:
- Sets `blocking = true` (or calls `DoBlock()`)
- Records a timestamp for grading

If the block system stores a similar "event time" reference, grading
happens in `GetResult()` or `IUpdatableUpdate()` by comparing the stored
press time against the event time — meaning pressing on frame 1 of the
block window records a very early timestamp and gets `SuccessBeforeEvent`,
while pressing near the event gets `SuccessPerfect`.

**Next: decompile `TimedBlockHandler.OnInputPressed()` (RVA `0xD9D290`)
and `TimedBlockHandler.IUpdatableUpdate()` (RVA `0xD9C410`).**
