using HarmonyLib;

namespace PerfectTimingArcaneBarrage.Patches;

// =============================================================================
//  LOGGING PATCHES — PotionKick combo
//
//  Log levels:
//    LogI  – key lifecycle events (lob, kick, impact, results)
//    LogD  – per-frame / high-frequency calls (UpdateKickForPlayer, Process*)
// =============================================================================


// ──────────────────────────────────────────────────────────────────────
// PotionKick  (the PlayerCombatMove driving the whole combo)
// ──────────────────────────────────────────────────────────────────────

/// <summary>OnLobPotion — Resh'an throws one potion; fires once per potion in the volley.</summary>
[HarmonyPatch(typeof(PotionKick), "OnLobPotion")]
static class Log_PotionKick_OnLobPotion
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogI(
            $"[PotionKick] >> OnLobPotion | this={__instance.Pointer:X} " +
            $"amountToThrow={__instance.potionAmountToThrow}");

    static void Postfix(PotionKick __instance)
        => Plugin.LogI(
            $"[PotionKick] << OnLobPotion | this={__instance.Pointer:X}");
}

/// <summary>OnKick — the shared kickCallback target; fires when any player's kick is accepted.</summary>
[HarmonyPatch(typeof(PotionKick), nameof(PotionKick.OnKick))]
static class Log_PotionKick_OnKick
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogI(
            $"[PotionKick] >> OnKick | this={__instance.Pointer:X}");

    static void Postfix(PotionKick __instance)
        => Plugin.LogI(
            $"[PotionKick] << OnKick | this={__instance.Pointer:X}");
}

/// <summary>UpdateKickForPlayer — per-frame, per-SPP-player QTE detection. High frequency → Debug.</summary>
[HarmonyPatch(typeof(PotionKick), "UpdateKickForPlayer",
    new System.Type[] { typeof(SinglePlayerPlusPlayer) })]
static class Log_PotionKick_UpdateKickForPlayer
{
    static void Prefix(PotionKick __instance, SinglePlayerPlusPlayer __0)
        => Plugin.LogD(
            $"[PotionKick] >> UpdateKickForPlayer | this={__instance.Pointer:X} " +
            $"player={(__0 != null ? __0.Pointer.ToString("X") : "null")}");

    static void Postfix(PotionKick __instance)
        => Plugin.LogD(
            $"[PotionKick] << UpdateKickForPlayer | this={__instance.Pointer:X}");
}

/// <summary>KickPotion — physically launches a potion projectile toward the enemy.</summary>
[HarmonyPatch(typeof(PotionKick), "KickPotion",
    new System.Type[] { typeof(Projectile) })]
static class Log_PotionKick_KickPotion
{
    static void Prefix(PotionKick __instance, Projectile __0)
        => Plugin.LogI(
            $"[PotionKick] >> KickPotion | this={__instance.Pointer:X} " +
            $"potion={(__0 != null ? __0.Pointer.ToString("X") : "null")}");

    static void Postfix(PotionKick __instance)
        => Plugin.LogI(
            $"[PotionKick] << KickPotion | this={__instance.Pointer:X}");
}

/// <summary>HasAPlayerKickedPotion — per-frame proximity query; Debug level.</summary>
[HarmonyPatch(typeof(PotionKick), "HasAPlayerKickedPotion",
    new System.Type[] { typeof(Projectile) })]
static class Log_PotionKick_HasAPlayerKickedPotion
{
    static void Postfix(Projectile __0, bool __result)
        => Plugin.LogD(
            $"[PotionKick] << HasAPlayerKickedPotion | " +
            $"potion={(__0 != null ? __0.Pointer.ToString("X") : "null")} result={__result}");
}

/// <summary>HasEveryPlayerKickedPotion — per-frame; Debug level.</summary>
[HarmonyPatch(typeof(PotionKick), "HasEveryPlayerKickedPotion",
    new System.Type[] { typeof(Projectile) })]
static class Log_PotionKick_HasEveryPlayerKickedPotion
{
    static void Postfix(Projectile __0, bool __result)
        => Plugin.LogD(
            $"[PotionKick] << HasEveryPlayerKickedPotion | " +
            $"potion={(__0 != null ? __0.Pointer.ToString("X") : "null")} result={__result}");
}

/// <summary>ProcessSuccessfulKicks — batch finalization after player loop; per-frame.</summary>
[HarmonyPatch(typeof(PotionKick), "ProcessSuccessfulKicks")]
static class Log_PotionKick_ProcessSuccessfulKicks
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogD($"[PotionKick] >> ProcessSuccessfulKicks | this={__instance.Pointer:X}");
}

/// <summary>ProcessOutdatedPotions — cleans up potions that expired or missed; per-frame.</summary>
[HarmonyPatch(typeof(PotionKick), "ProcessOutdatedPotions")]
static class Log_PotionKick_ProcessOutdatedPotions
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogD($"[PotionKick] >> ProcessOutdatedPotions | this={__instance.Pointer:X}");
}

/// <summary>ProcessPotionsKickedByAllPlayers — finalises potions where all players recorded a result; per-frame.</summary>
[HarmonyPatch(typeof(PotionKick), "ProcessPotionsKickedByAllPlayers")]
static class Log_PotionKick_ProcessPotionsKickedByAllPlayers
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogD($"[PotionKick] >> ProcessPotionsKickedByAllPlayers | this={__instance.Pointer:X}");
}

/// <summary>OnPotionReachedEnemy — a potion completed its trajectory (kicked or missed).</summary>
[HarmonyPatch(typeof(PotionKick), "OnPotionReachedEnemy",
    new System.Type[] { typeof(Projectile) })]
static class Log_PotionKick_OnPotionReachedEnemy
{
    static void Prefix(PotionKick __instance, Projectile __0)
        => Plugin.LogI(
            $"[PotionKick] >> OnPotionReachedEnemy | this={__instance.Pointer:X} " +
            $"potion={(__0 != null ? __0.Pointer.ToString("X") : "null")}");
}

/// <summary>OnFinalKickDone — all potions have been resolved; move about to end.</summary>
[HarmonyPatch(typeof(PotionKick), "OnFinalKickDone")]
static class Log_PotionKick_OnFinalKickDone
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogI($"[PotionKick] >> OnFinalKickDone | this={__instance.Pointer:X}");
}

/// <summary>OnPotionsDone — potionDoneCount reached potionAmountToThrow; triggers move end.</summary>
[HarmonyPatch(typeof(PotionKick), "OnPotionsDone")]
static class Log_PotionKick_OnPotionsDone
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogI($"[PotionKick] >> OnPotionsDone | this={__instance.Pointer:X}");
}

/// <summary>GetQTEResult — aggregates all per-potion TeamQTEResults into the move's overall result.</summary>
[HarmonyPatch(typeof(PotionKick), "GetQTEResult")]
static class Log_PotionKick_GetQTEResult
{
    static void Prefix(PotionKick __instance)
        => Plugin.LogI($"[PotionKick] >> GetQTEResult | this={__instance.Pointer:X}");

    static void Postfix(TeamQTEResult __result)
        => Plugin.LogI(
            $"[PotionKick] << GetQTEResult | " +
            $"result={(__result != null ? $"qteId={__result.QteId} hasSuccess={__result.HasSuccess()}" : "null")}");
}


// ──────────────────────────────────────────────────────────────────────
// KickPotionState  (Seraï's state machine state during kick window)
// ──────────────────────────────────────────────────────────────────────

/// <summary>OnKickInput — Seraï (player 1) accepted a kick input.</summary>
[HarmonyPatch(typeof(KickPotionState), nameof(KickPotionState.OnKickInput))]
static class Log_KickPotionState_OnKickInput
{
    static void Prefix(KickPotionState __instance)
        => Plugin.LogI(
            $"[KickPotionState] >> OnKickInput | this={__instance.Pointer:X} " +
            $"kickWindowDuration={__instance.kickWindowDuration:F3} " +
            $"kickCooldown={__instance.kickCooldown:F3}");

    static void Postfix(KickPotionState __instance)
        => Plugin.LogI(
            $"[KickPotionState] << OnKickInput | this={__instance.Pointer:X}");
}


// ──────────────────────────────────────────────────────────────────────
// SPPPlayerPotionKickState  (co-op second player's state during kick window)
// ──────────────────────────────────────────────────────────────────────

/// <summary>StateEnter — kick window opened for the second player.</summary>
[HarmonyPatch(typeof(SPPPlayerPotionKickState), nameof(SPPPlayerPotionKickState.StateEnter))]
static class Log_SPPPlayerPotionKickState_StateEnter
{
    static void Prefix(SPPPlayerPotionKickState __instance)
        => Plugin.LogI(
            $"[SPPPlayerPotionKickState] >> StateEnter | this={__instance.Pointer:X} " +
            $"kickWindowDuration={__instance.kickWindowDuration:F3} " +
            $"kickCooldown={__instance.kickCooldown:F3} " +
            $"kickCallbackSet={__instance.kickCallback != null}");

    static void Postfix(SPPPlayerPotionKickState __instance)
        => Plugin.LogI(
            $"[SPPPlayerPotionKickState] << StateEnter | this={__instance.Pointer:X}");
}
