# `SunboyShootSunballState.StateExecute` — Analysis

## Context

`SunboyShootSunballState` is the **non-QTE, automatic** sunball charge path — used
when Soonrang fires without a player-managed charge input (e.g. the standalone
Sunboy actor firing independently, or `throwSunballWhenReady = true`).
Its `StateExecute` is the per-frame update. Compare to `SunboyShootQTESunballState`,
which has the player hold-A input loop.

---

## Pointer Arithmetic Key

`param_1` is `undefined1(*)[16]` — 16-byte blocks. `param_1[N]` = `base + N×0x10`.
Sub-byte offsets within a block use standard `+k` syntax.

| Expression | Offset | C# field | Type |
|---|---|---|---|
| `param_1[0xC][0xA]` = `+0xCA` | `sunballCharging` | bool | Main gate |
| `param_1[0xC][8]` = `+0xC8` | `updateRecoil` | bool | |
| `*(float*)param_1[0xC]` = `+0xC0` | `sunballCharge` | float | 0→1 |
| `*(float*)(param_1[8]+4)` = `+0x84` | `sunballChargeDuration` | float | seconds |
| `*(longlong**)param_1[6]` = `+0x60` | `sunballChargeLevelRTPC` | RTPC | |
| `*(ptr*)param_1[0xF]` = `+0xF0` | `sunballProjectileInstance` | SunballProjectile* | |
| `*(ptr*)(param_1[0xA]+8)` = `+0xA8` | `onSunballReady` | Action | |
| `param_1[8][0]` = `+0x80` | `throwSunballWhenReady` | bool | |
| `param_1[0xB][0xC]` = `+0xBC` | `readyToShoot` | bool | |
| `*(int*)(param_1[9]+4)` = `+0x94` | `sunballMaxLevel` | int | |

For `SunballProjectile proj` (pointer `pcVar7`):

| Expression | Offset | C# field |
|---|---|---|
| `*(int*)(*(ptr@proj+0xC0) + 0xC)` | `proj+0xCC` | `level` (int) ★ |

★ Ghidra decodes `proj+0xCC` as accessing the 16-byte block starting at `proj+0xC0`
(`stopSunballMovement: Event`), then byte `+0xC` within it. This is artifact
notation for the flat read `*(int*)(proj + 0xCC)` = `level`.

---

## Annotated C# Reconstruction

```csharp
public override void StateExecute()
{
    // ── Early exits ──────────────────────────────────────────────────────────
    if (!sunballCharging)    // +0xCA: cleared once shot is armed
    {
        if (updateRecoil)    // +0xC8
            UpdateRecoil();
        return;
    }

    // ── Advance charge by time ────────────────────────────────────────────────
    // Linear 0→1 over sunballChargeDuration seconds. No player input involved.
    sunballCharge += Time.deltaTime / sunballChargeDuration;   // +0xC0 / +0x84

    SunballProjectile proj = sunballProjectileInstance;   // +0xF0
    RTPC  rtpc             = sunballChargeLevelRTPC;      // +0x60

    if (proj == null) throw NullReferenceException();

    if (rtpc != null)
    {
        // ── Wwise audio ───────────────────────────────────────────────────────
        RTPC.SetValue(rtpc, proj.gameObject, sunballCharge * 100f);

        if (proj == null) throw NullReferenceException();

        // ── Visual sync ───────────────────────────────────────────────────────
        proj.SetCharge(sunballCharge);   // drives light, pullback, animator

        if (proj == null) throw NullReferenceException();

        // ── Auto-advance level based on charge fraction ───────────────────────
        // targetLevel = floor(maxLevel × charge). Levels step up 1-at-a-time
        // each frame until the projectile level equals the fractional target.
        int targetLevel = (int)((float)sunballMaxLevel * sunballCharge);
        if (proj.level < targetLevel)
            proj.SetLevel(proj.level + 1);

        // ── Charge complete ───────────────────────────────────────────────────
        if (sunballCharge >= 1.0f)
        {
            // Fire onSunballReady exactly once (readyToShoot guards re-entry).
            if (!readyToShoot && onSunballReady != null)   // +0xBC, +0xA8
                onSunballReady.Invoke();

            sunballCharging = false;   // +0xCA — stops per-frame update
            readyToShoot    = true;    // +0xBC — signals callers the ball is ready

            if (throwSunballWhenReady)   // +0x80
                ThrowSunball(/* normalizedTime */);
        }
    }
}
```

---

## Key Differences vs. QTE Version

| Aspect | `SunboyShootSunballState` (this) | `SunboyShootQTESunballState` |
|---|---|---|
| Charge source | `Time.deltaTime / sunballChargeDuration` — automatic | Player holds A, each frame of button-hold adds charge |
| Level advancement | Auto: `level < floor(maxLevel × charge)` → `SetLevel(level+1)` | Discrete steps — `SetLevel` called at each step boundary |
| Peak hold window | None — charge goes straight to 1.0 and fires | `qteFullChargeStepDuration` timer at max level |
| Player input | Not read | `InputCategory.GetButton("Attack")` polled each frame |
| Success condition | `charge >= 1.0f` → always full power | Released at `level == maxLevel`; else sub-max shot |
| `playerInputDone` flag | Does not exist on this class | +0x134, central to QTE decision |
| fallback "miss" path | None — always fires at max level if charge completes | `shootOnNextChargeStep` descent to `level-1` |

---

## Level Auto-Advance Formula

```
targetLevel = floor(sunballMaxLevel × sunballCharge)
```

With `sunballMaxLevel = 4` and a linear charge over `sunballChargeDuration` seconds:

| Charge | targetLevel | Transition |
|---|---|---|
| 0.00 – 0.24 | 0 | (SetLevel never called — starts at 1 via Init) |
| 0.25 – 0.49 | 1 | → Level 1 |
| 0.50 – 0.74 | 2 | → Level 2 |
| 0.75 – 0.99 | 3 | → Level 3 |
| 1.00 | 4 | → Level 4, then fires |

The step-up each frame is limited to `+1` by the `SetLevel(proj.level + 1)` call,
so if multiple thresholds are crossed in a single (very long) frame the level
catches up one step at a time across successive executes.

---

## `onSunballReady` Delegate Invocation

```c
lVar6 = *(longlong *)(param_1[10] + 8);   // = onSunballReady Action ptr at +0xA8
if (!readyToShoot && lVar6 != 0)
{
    (**(code**)(lVar6 + 0x18))(*(undefined8*)(lVar6 + 0x40));
}
```

A C# `Action` delegate in IL2CPP:
- `+0x18`: `invoke` method pointer (`Invoke_Action`)
- `+0x40`: bound target (`_target` — the captured `this` of the lambda)

This is the standard IL2CPP delegate dispatch pattern. The callback fires
exactly once because `readyToShoot` is checked before and set immediately after.

---

## Mod Implications

This class is irrelevant for the auto-release patch being developed.
The Soonrang QTE sunball charge (player-facing) is handled entirely by
`SunboyShootQTESunballState`, not this class. `SunboyShootSunballState` is
used for the **Sunboy** standalone actor's non-interactive charge —
it always reaches max level and there is no release timing to optimise.
