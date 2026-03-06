# `TimedBlockHandler.DoBlock()` — Ghidra Analysis

RVA `0xD9D3E0` / Offset `0xD9BBE0` — confirmed match from dump.cs.

---

## Field offset map (`param_1` = `this`)

| Ghidra expression | Byte offset | Field (from dump) | Type |
|---|---|---|---|
| `*(char *)((longlong)param_1 + 0x3d)` | `0x3D` | `playingBlockAnimation` | `bool` |
| `*(float *)(param_1 + 7)` | `7 × 8 = 0x38` | `blockEndTime` | `float` |
| `*(float *)(param_1 + 8)` | `8 × 8 = 0x40` | `blockAnimationEndTime` | `float` |
| `*(undefined2 *)((longlong)param_1 + 0x3c)` | `0x3C–0x3D` | `blocking` + `playingBlockAnimation` | `bool, bool` |
| `param_1[3]` | `3 × 8 = 0x18` | `combatActor` | `PlayerCombatActor` |

---

## Annotated pseudocode

```c
void DoBlock(TimedBlockHandler* self, ...)
{
    // ── Static initialiser (IL2CPP class init) ──────────────────────────
    if (!classInitialised) {
        RunStaticConstructor();
        classInitialised = true;
    }

    // ── Re-entry guard ──────────────────────────────────────────────────
    // Returns immediately if block animation is already in progress.
    // This is the ONLY guard — `blocking` is NOT checked here.
    if (self->playingBlockAnimation != false)
        return;

    // ── Get current time ────────────────────────────────────────────────
    // Resolves and caches UnityEngine.Time::get_time() via IL2CPP lookup.
    float now = Time.time;

    // ── Compute blockEndTime ────────────────────────────────────────────
    // GetBlockDuration() (RVA 0xD9D840) returns the duration of the
    // hold-block window in seconds.
    float blockEndTime = now + GetBlockDuration();
    self->blockEndTime = blockEndTime;              // offset 0x38

    // ── Compute blockAnimationEndTime ───────────────────────────────────
    // FUN_1800127d0 returns some component/actor reference (lVar2).
    // *(longlong*)(lVar2 + 0xe0) is likely a reference to the attack's
    // animation event or move definition.
    // *(float*)(that + 0x40) is probably the attack animation event time.
    // Result: blockAnimationEndTime = blockEndTime − animEventTime
    var actor = FUN_1800127d0(self, ...);
    if (actor != null && *(actor + 0xe0) != null) {
        float animEventTime = *(float*)(*(actor + 0xe0) + 0x40);
        self->blockAnimationEndTime = blockEndTime - animEventTime;  // offset 0x40

        // ── Trigger block animation on combatActor ──────────────────────
        // plVar3 = self->combatActor (offset 0x18)
        // Virtual call at vtable offset 0x5b8, passing value at 0x5c0.
        // Likely: combatActor.PlayBlockAnimation(blockAnimatorState)
        PlayerCombatActor* combatActor = self->combatActor;
        if (combatActor != null) {
            combatActor->vtable[0x5b8](combatActor, combatActor->vtable[0x5c0]);

            // ── Set blocking flags ──────────────────────────────────────
            // Writes 0x0101 as a 2-byte value at offset 0x3C:
            //   blocking            (0x3C) = true
            //   playingBlockAnimation (0x3D) = true
            self->blocking = true;
            self->playingBlockAnimation = true;
            return;
        }
    }

    // Error path — combat actor or animation reference was null.
    FatalError();
}
```

---

## Key conclusions

### 1. `DoBlock()` does NOT determine QTE result quality

There is no comparison of `Time.time` against a "perfect window" here.
`DoBlock()` only handles **animation state** — it sets `blockEndTime`,
`blockAnimationEndTime`, triggers the animation, and sets the two flags.

The QTE result (`SuccessPerfect`, `SuccessBeforeEvent`, etc.) is recorded
**before** `DoBlock()` is called, almost certainly in `OnInputPressed()` or
`IUpdatableUpdate()`, by comparing `Time.time` against the attack's event
timestamp.

### 2. The re-entry guard is `playingBlockAnimation`, not `blocking`

`CanPressInput()` (offset `0xD9D4D0`) likely checks `blocking` (0x3C).
`DoBlock()` checks `playingBlockAnimation` (0x3D).

These are set **simultaneously** at the end of a successful `DoBlock()` call
(`0x3C–0x3D = 0x0101`), but they may be **cleared independently**:
- `EndBlock()` (RVA `0xD9C120`) probably clears `blocking` when the hold expires.
- `StopBlockAnimation()` (RVA `0xD9D270`) probably clears `playingBlockAnimation`.

If `blocking` is cleared before `playingBlockAnimation`, `CanPressInput()`
re-opens while `DoBlock()` is still guarded — explaining why `OnInputPressed`
fires 9 times in the log but only the first press matters for the animation.

### 3. The `blockAnimationEndTime` subtraction is the only timing math visible

`blockAnimationEndTime = blockEndTime − animEventTime` — this governs when
the block animation ends, not which QTE grade is awarded.

---

## What to look at next

- **`OnInputPressed()` (RVA `0xD9D290`)** — this is where the QTE result
  quality is almost certainly computed and stored in `inputResults`.
- **`IUpdatableUpdate()` (RVA `0xD9C410`)** — contains the auto-time branch
  (inlined `CanAutoTimeBlock`) and calls `TestInputPress` → `GetInputDown` →
  `CanPressInput` → `OnInputPressed`.
- **`CanPressInput()` (RVA `0xD9D4D0`)** — determines the re-press gate;
  understanding when it re-opens explains the multi-press problem.
