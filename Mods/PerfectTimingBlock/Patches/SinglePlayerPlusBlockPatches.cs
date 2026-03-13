using HarmonyLib;

namespace PerfectTimingBlock.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// SinglePlayerPlusBlock patches
//
// SinglePlayerPlusBlock is the co-op equivalent of TimedBlockHandler, used when
// SinglePlayerPlus mode is active. It has its own DoBlock() / GetResult() pair
// with identical grading logic (blocking field → SuccessBeforeEvent or
// FailDidNoPress).
// ─────────────────────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(SinglePlayerPlusBlock), nameof(SinglePlayerPlusBlock.GetResult))]
static class Patch_SinglePlayerPlusBlock_GetResult
{
    /// <summary>
    /// Call DoBlock() before grading so blocking == true when GetResult() reads it.
    /// DoBlock() is idempotent — playingBlockAnimation guards against re-entry.
    /// </summary>
    static void Prefix(SinglePlayerPlusBlock __instance)
    {
        __instance.DoBlock();
    }

    /// <summary>Log the result for diagnostic purposes.</summary>
    static void Postfix(SinglePlayerPlusBlock __instance, QTEResult __result)
    {
        Plugin.LogI(
            $"[SPPBlock.GetResult] POST | result={__result.result} " +
            $"isSuccess={__result.IsSuccess()} " +
            $"owner={(__result.owner != null ? __result.owner.ToString() : "null")}");
    }
}
