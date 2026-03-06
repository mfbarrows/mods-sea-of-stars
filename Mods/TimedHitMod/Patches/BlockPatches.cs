using HarmonyLib;

namespace TimedHitMod.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// Block auto-time patches
//
// CanAutoTimeBlock is inlined by IL2CPP into IUpdatableUpdate and cannot be
// patched directly.
//
// Approach: prefix TimedBlockHandler.GetResult() and call DoBlock() before the
// grading logic runs. GetResult() checks the `blocking` field (0x3C) to assign
// SuccessBeforeEvent (true) or FailDidNoPress (false). DoBlock() sets blocking
// = true and is idempotent — its playingBlockAnimation guard (0x3D) prevents
// re-entry if it was already triggered by a real button press.
//
// AutoTimeBlockModifier.CanAutoTime() is kept as a secondary patch for cases
// where the relic is equipped (mirrors the attack-side approach exactly).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Prefix TimedBlockHandler.GetResult to call DoBlock() before grading.
/// This guarantees blocking == true when the grade is computed.
/// Signature: public TeamQTEResult GetResult()
/// </summary>
[HarmonyPatch(typeof(TimedBlockHandler), nameof(TimedBlockHandler.GetResult))]
static class Patch_TimedBlockHandler_GetResult
{
    static void Prefix(TimedBlockHandler __instance)
    {
        __instance.DoBlock();
    }
}

/// <summary>
/// Force AutoTimeBlockModifier.CanAutoTime to always return true.
/// Secondary patch — fires only when a modifier instance exists (relic equipped).
/// Signature: public bool CanAutoTime()
/// </summary>
[HarmonyPatch(typeof(AutoTimeBlockModifier), nameof(AutoTimeBlockModifier.CanAutoTime))]
static class Patch_AutoTimeBlockModifier_CanAutoTime
{
    static void Postfix(ref bool __result)
    {
        __result = true;
    }
}

// GetInputDown patch disabled — GetResult prefix is cleaner and fires once.
// Kept here for reference if needed.
//
// [HarmonyPatch(typeof(TimedBlockHandler), "GetInputDown")]
// static class Patch_TimedBlockHandler_GetInputDown
// {
//     static void Postfix(ref bool __result) { __result = true; }
// }

