using HarmonyLib;

namespace TimedHitMod.Patches;

// =============================================================================
//  LOGGING PATCHES
//  Prefix  -> logs intent / incoming arguments before original method runs.
//  Postfix -> logs result / state after original method ran.
//
//  Log levels used:
//    LogInfo  – important lifecycle events (window open/close, results)
//    LogDebug – high-frequency or supporting detail (CanPressInput, modifiers)
// =============================================================================


// ──────────────────────────────────────────────────────────────────────
// AbstractTimedAttackHandler  (shared base for all attack-side handlers)
// ──────────────────────────────────────────────────────────────────────

/// <summary>BeginInputPhase -- the QTE timing window has opened for an attack.</summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), nameof(AbstractTimedAttackHandler.BeginInputPhase))]
static class Log_AttackHandler_BeginInputPhase
{
    static void Prefix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[Attack.BeginInputPhase] PRE  | handler={__instance.GetType().Name} " +
            $"windowMult={__instance.WindowDurationMultiplier:F2}");

    static void Postfix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[Attack.BeginInputPhase] POST | handler={__instance.GetType().Name} -- window is now open");
}

/// <summary>EndInputPhase -- the QTE timing window has closed.</summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), nameof(AbstractTimedAttackHandler.EndInputPhase))]
static class Log_AttackHandler_EndInputPhase
{
    static void Prefix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[Attack.EndInputPhase] PRE  | handler={__instance.GetType().Name}");

    static void Postfix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[Attack.EndInputPhase] POST | handler={__instance.GetType().Name} -- window closed");
}

/// <summary>OnInputPressed -- the player pressed the button during an attack window.</summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), nameof(AbstractTimedAttackHandler.OnInputPressed))]
static class Log_AttackHandler_OnInputPressed
{
    static void Prefix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[Attack.OnInputPressed] PRE  | handler={__instance.GetType().Name} " +
            $"doQTEFeedback={__instance.doQTEResultFeedback}");

    static void Postfix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[Attack.OnInputPressed] POST | handler={__instance.GetType().Name} -- press registered");
}

/// <summary>
/// CanPressInput -- gate called every update frame while the window is open.
/// Logged at Debug to avoid spam; flip to LogInfo if you need more detail.
/// </summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanPressInput")]
static class Log_AttackHandler_CanPressInput
{
    static void Prefix(AbstractTimedAttackHandler __instance)
        => Plugin.LogD(
            $"[Attack.CanPressInput] PRE  | handler={__instance.GetType().Name}");

    static void Postfix(AbstractTimedAttackHandler __instance, bool __result)
        => Plugin.LogD(
            $"[Attack.CanPressInput] POST | handler={__instance.GetType().Name} result={__result}");
}


// ──────────────────────────────────────────────────────────────────────
// TimedAttackHandler  (concrete multi-player attack QTE handler)
// ──────────────────────────────────────────────────────────────────────

/// <summary>GetResult -- starts listening for player input and wires up the result callback.</summary>
[HarmonyPatch(typeof(TimedAttackHandler), "GetResult")]
static class Log_TimedAttackHandler_GetResult
{
    static void Prefix(TimedAttackHandler __instance, PlayerCombatMoveDefinition __0)
        => Plugin.LogI(
            $"[TimedAttackHandler.GetResult] PRE  | move={((__0 != null) ? __0.name : "null")}");

    static void Postfix(TimedAttackHandler __instance, PlayerCombatMoveDefinition __0)
        => Plugin.LogI(
            $"[TimedAttackHandler.GetResult] POST | move={((__0 != null) ? __0.name : "null")} -- QTE armed");
}

/// <summary>OnAttackResultReady -- fires once per-player with their individual QTE result.</summary>
[HarmonyPatch(typeof(TimedAttackHandler), "OnAttackResultReady")]
static class Log_TimedAttackHandler_OnAttackResultReady
{
    static void Prefix(TimedAttackHandler __instance, Rewired.Player __0, EQTEResult __1)
        => Plugin.LogI(
            $"[TimedAttackHandler.OnAttackResultReady] PRE  | " +
            $"player={(__0 != null ? __0.name : "null")} result={__1}");

    static void Postfix(TimedAttackHandler __instance, Rewired.Player __0, EQTEResult __1)
        => Plugin.LogI(
            $"[TimedAttackHandler.OnAttackResultReady] POST | " +
            $"player={(__0 != null ? __0.name : "null")} result={__1} -- recorded");
}

/// <summary>SendAttackResult (private) -- broadcasts the aggregated TeamQTEResult to the game.</summary>
[HarmonyPatch(typeof(TimedAttackHandler), "SendAttackResult")]
static class Log_TimedAttackHandler_SendAttackResult
{
    static void Prefix(TimedAttackHandler __instance)
        => Plugin.LogI(
            $"[TimedAttackHandler.SendAttackResult] PRE  | handler={__instance.GetType().Name} -- about to broadcast result");

    static void Postfix(TimedAttackHandler __instance)
        => Plugin.LogI(
            $"[TimedAttackHandler.SendAttackResult] POST | handler={__instance.GetType().Name} -- result broadcast complete");
}


// ──────────────────────────────────────────────────────────────────────
// TimedBlockHandler  (concrete block / defensive QTE handler)
// ──────────────────────────────────────────────────────────────────────

/// <summary>BeginInputPhase -- the block QTE window has opened.</summary>
[HarmonyPatch(typeof(TimedBlockHandler), nameof(TimedBlockHandler.BeginInputPhase))]
static class Log_BlockHandler_BeginInputPhase
{
    static void Prefix(TimedBlockHandler __instance)
        => Plugin.LogI(
            $"[Block.BeginInputPhase] PRE  | windowMult={__instance.WindowDurationMultiplier:F2}");

    static void Postfix(TimedBlockHandler __instance)
        => Plugin.LogI(
            $"[Block.BeginInputPhase] POST | block window open");
}

/// <summary>EndInputPhase -- the block QTE window has closed.</summary>
[HarmonyPatch(typeof(TimedBlockHandler), nameof(TimedBlockHandler.EndInputPhase))]
static class Log_BlockHandler_EndInputPhase
{
    static void Prefix(TimedBlockHandler __instance)
        => Plugin.LogI(
            $"[Block.EndInputPhase] PRE  | handler={__instance.GetType().Name}");

    static void Postfix(TimedBlockHandler __instance)
        => Plugin.LogI(
            $"[Block.EndInputPhase] POST | block window closed");
}

/// <summary>OnInputPressed -- the player pressed the button during a block window.</summary>
[HarmonyPatch(typeof(TimedBlockHandler), nameof(TimedBlockHandler.OnInputPressed))]
static class Log_BlockHandler_OnInputPressed
{
    static void Prefix(TimedBlockHandler __instance)
        => Plugin.LogI(
            $"[Block.OnInputPressed] PRE  | handler={__instance.GetType().Name}");

    static void Postfix(TimedBlockHandler __instance)
        => Plugin.LogI(
            $"[Block.OnInputPressed] POST | press registered");
}
/// <summary>
/// OnInputPressed -- co-op block handler (SinglePlayerPlusBlock).
/// Separate from TimedBlockHandler; used when SinglePlayerPlus mode is active.
/// </summary>
[HarmonyPatch(typeof(SinglePlayerPlusBlock), nameof(SinglePlayerPlusBlock.OnInputPressed))]
static class Log_SinglePlayerPlusBlock_OnInputPressed
{
    static void Prefix(SinglePlayerPlusBlock __instance)
        => Plugin.LogI(
            $"[SPPBlock.OnInputPressed] PRE  | handler={__instance.GetType().Name}");

    static void Postfix()
        => Plugin.LogI(
            $"[SPPBlock.OnInputPressed] POST | press registered");
}
/// <summary>
/// GetInputDown -- physical-input check polled every update frame.
/// Logged at Debug level to confirm it is NOT being reached by the auto-time path.
/// </summary>
[HarmonyPatch(typeof(TimedBlockHandler), "GetInputDown")]
static class Log_BlockHandler_GetInputDown
{
    static void Postfix(bool __result)
        => Plugin.LogD(
            $"[Block.GetInputDown] POST | result={__result}");
}

/// <summary>GetResult -- returns the TeamQTEResult for this block attempt.</summary>
[HarmonyPatch(typeof(TimedBlockHandler), nameof(TimedBlockHandler.GetResult))]
static class Log_BlockHandler_GetResult
{
    static void Prefix(TimedBlockHandler __instance)
        => Plugin.LogI(
            $"[Block.GetResult] PRE  | handler={__instance.GetType().Name}");

    static void Postfix(TeamQTEResult __result)
        => Plugin.LogI(
            $"[Block.GetResult] POST | result={((__result != null) ? "non-null" : "null")}");
}


// ──────────────────────────────────────────────────────────────────────
// AutoTimeAttackModifier  (ScriptableObject modifier that backs the relic)
// ──────────────────────────────────────────────────────────────────────

/// <summary>CanAutoTimeHit -- protected method on AbstractTimedAttackHandler, patched to force true.</summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanAutoTimeHit")]
static class Log_AbstractTimedAttackHandler_CanAutoTimeHit
{
    static void Postfix(AbstractTimedAttackHandler __instance, bool __result)
        => Plugin.LogD(
            $"[CanAutoTimeHit] POST | handler={__instance.GetType().Name} result={__result}");
}

/// <summary>CanAutoTime -- called by CanAutoTimeHit for each active modifier to decide auto-timing.</summary>
[HarmonyPatch(typeof(AutoTimeAttackModifier), nameof(AutoTimeAttackModifier.CanAutoTime))]
static class Log_AutoTimeAttackModifier_CanAutoTime
{
    static void Prefix(AutoTimeAttackModifier __instance, PlayerCombatMoveDefinition __0)
        => Plugin.LogD(
            $"[AutoTimeAttackModifier.CanAutoTime] PRE  | " +
            $"chances={__instance.autoTimeChances:F2} move={((__0 != null) ? __0.name : "null")}");

    static void Postfix(AutoTimeAttackModifier __instance, bool __result)
        => Plugin.LogD(
            $"[AutoTimeAttackModifier.CanAutoTime] POST | " +
            $"chances={__instance.autoTimeChances:F2} result={__result}");
}


// ──────────────────────────────────────────────────────────────────────
// AutoTimeBlockModifier  (ScriptableObject modifier that backs the relic)
// ──────────────────────────────────────────────────────────────────────

/// <summary>CanAutoTime -- called by CanAutoTimeBlock to decide auto-timing for blocks.</summary>
[HarmonyPatch(typeof(AutoTimeBlockModifier), nameof(AutoTimeBlockModifier.CanAutoTime))]
static class Log_AutoTimeBlockModifier_CanAutoTime
{
    static void Prefix(AutoTimeBlockModifier __instance)
        => Plugin.LogD(
            $"[AutoTimeBlockModifier.CanAutoTime] PRE  | chances={__instance.autoTimeChances:F2}");

    static void Postfix(AutoTimeBlockModifier __instance, bool __result)
        => Plugin.LogD(
            $"[AutoTimeBlockModifier.CanAutoTime] POST | chances={__instance.autoTimeChances:F2} result={__result}");
}


// ──────────────────────────────────────────────────────────────────────
// TeamQTEResult  (aggregated result consumed by damage/MP calculations)
// ──────────────────────────────────────────────────────────────────────

/// <summary>GetBestResult -- final result read by damage, MP reward, and animation systems.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.GetBestResult))]
static class Log_TeamQTEResult_GetBestResult
{
    static void Prefix(TeamQTEResult __instance)
        => Plugin.LogD(
            $"[TeamQTEResult.GetBestResult] PRE  | qteId={__instance.QteId}");

    static void Postfix(QTEResult __result)
        => Plugin.LogD(
            $"[TeamQTEResult.GetBestResult] POST | best={__result.result}");
}

/// <summary>HasSuccess -- broad success gate used by many callers before reading the result.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.HasSuccess))]
static class Log_TeamQTEResult_HasSuccess
{
    static void Prefix(TeamQTEResult __instance)
        => Plugin.LogD(
            $"[TeamQTEResult.HasSuccess] PRE  | qteId={__instance.QteId}");

    // Postfix omitted -- calling other patched methods from here causes re-entrant crashes.
}

/// <summary>GetSuccessCount -- used by multi-player checks (e.g. triple-block cancel).</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.GetSuccessCount))]
static class Log_TeamQTEResult_GetSuccessCount
{
    static void Prefix(TeamQTEResult __instance)
        => Plugin.LogD(
            $"[TeamQTEResult.GetSuccessCount] PRE  | qteId={__instance.QteId}");

    static void Postfix(int __result)
        => Plugin.LogD(
            $"[TeamQTEResult.GetSuccessCount] POST | count={__result}");
}

/// <summary>GetResultForPlayer -- per-player result lookup used in co-op / individual feedback.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.GetResultForPlayer))]
static class Log_TeamQTEResult_GetResultForPlayer
{
    static void Prefix(TeamQTEResult __instance, Rewired.Player __0)
        => Plugin.LogD(
            $"[TeamQTEResult.GetResultForPlayer] PRE  | qteId={__instance.QteId} " +
            $"player={(__0 != null ? __0.name : "null")}");

    // Postfix omitted -- QTEResult field access is safe but keeping symmetry with other simplifications.
}

/// <summary>HasSuccessForPlayer -- per-player success check.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.HasSuccessForPlayer))]
static class Log_TeamQTEResult_HasSuccessForPlayer
{
    static void Prefix(TeamQTEResult __instance, Rewired.Player __0)
        => Plugin.LogD(
            $"[TeamQTEResult.HasSuccessForPlayer] PRE  | qteId={__instance.QteId} " +
            $"player={(__0 != null ? __0.name : "null")}");

    // Postfix omitted -- keeping patch symmetric with HasSuccess simplification.
}


// ──────────────────────────────────────────────────────────────────────
// Block damage dispatch chain  (CombatDamage -> modifiers)
// ──────────────────────────────────────────────────────────────────────

/// <summary>ApplyBlockSuccessModifier -- applies damage cap and success bonuses when block succeeded.</summary>
[HarmonyPatch(typeof(CombatDamage), "ApplyBlockSuccessModifier")]
static class Log_CombatDamage_ApplyBlockSuccessModifier
{
    static void Prefix()
        => Plugin.LogI("[CombatDamage.ApplyBlockSuccessModifier] PRE  | applying block success modifiers");

    static void Postfix()
        => Plugin.LogI("[CombatDamage.ApplyBlockSuccessModifier] POST | done");
}

/// <summary>ApplyBlockFailedModifiers (private) -- applies penalty damage when block failed.</summary>
[HarmonyPatch(typeof(CombatDamage), "ApplyBlockFailedModifiers")]
static class Log_CombatDamage_ApplyBlockFailedModifiers
{
    static void Prefix()
        => Plugin.LogI("[CombatDamage.ApplyBlockFailedModifiers] PRE  | applying block FAIL modifiers -- should not appear with mod active");

    static void Postfix()
        => Plugin.LogI("[CombatDamage.ApplyBlockFailedModifiers] POST | done");
}

/// <summary>PlayerCombatActor.OnBlockInput -- fires when the block QTE window opens for a character.</summary>
[HarmonyPatch(typeof(PlayerCombatActor), nameof(PlayerCombatActor.OnBlockInput))]
static class Log_PlayerCombatActor_OnBlockInput
{
    static void Prefix(PlayerCombatActor __instance)
        => Plugin.LogI(
            $"[PlayerCombatActor.OnBlockInput] PRE  | actor={__instance.GetType().Name}");

    static void Postfix(PlayerCombatActor __instance)
        => Plugin.LogI(
            $"[PlayerCombatActor.OnBlockInput] POST | done");
}

/// <summary>PlayerCombatActor.GetBlockResult -- retrieves the block QTE result from the actor.</summary>
[HarmonyPatch(typeof(PlayerCombatActor), nameof(PlayerCombatActor.GetBlockResult))]
static class Log_PlayerCombatActor_GetBlockResult
{
    static void Prefix(PlayerCombatActor __instance)
        => Plugin.LogI(
            $"[PlayerCombatActor.GetBlockResult] PRE  | actor={__instance.GetType().Name}");

    static void Postfix(TeamQTEResult __result)
        => Plugin.LogI(
            $"[PlayerCombatActor.GetBlockResult] POST | result={((__result != null) ? "non-null" : "null")}");
}

/// <summary>TimedBlockFailedDamagePercentageModifier -- extra damage penalty on failed block.</summary>
[HarmonyPatch(typeof(TimedBlockFailedDamagePercentageModifier), nameof(TimedBlockFailedDamagePercentageModifier.GetModifiedDamage))]
static class Log_TimedBlockFailedDamage_GetModifiedDamage
{
    static void Prefix(float __0)
        => Plugin.LogI(
            $"[TimedBlockFailedDamage.GetModifiedDamage] PRE  | baseDamage={__0:F1} -- should not appear with mod active");

    static void Postfix(float __result)
        => Plugin.LogI(
            $"[TimedBlockFailedDamage.GetModifiedDamage] POST | modifiedDamage={__result:F1}");
}

/// <summary>TimedBlockAbsoluteDamageCapModifier -- caps damage on a successful block.</summary>
[HarmonyPatch(typeof(TimedBlockAbsoluteDamageCapModifier), nameof(TimedBlockAbsoluteDamageCapModifier.GetModifiedDamage))]
static class Log_TimedBlockDamageCap_GetModifiedDamage
{
    static void Prefix(float __0)
        => Plugin.LogI(
            $"[TimedBlockDamageCap.GetModifiedDamage] PRE  | baseDamage={__0:F1}");

    static void Postfix(float __result)
        => Plugin.LogI(
            $"[TimedBlockDamageCap.GetModifiedDamage] POST | modifiedDamage={__result:F1}");
}

// ──────────────────────────────────────────────────────────────────────
// TimeQTESettings / TimeQTEHandler  (attack-QTE timing windows)
// ──────────────────────────────────────────────────────────────────────

/// <summary>
/// TimeQTEHandler.Init -- logs the concrete window values whenever a QTE
/// handler is initialised, once per attack QTE object.
/// </summary>
[HarmonyPatch(typeof(TimeQTEHandler), nameof(TimeQTEHandler.Init))]
static class Log_TimeQTEHandler_Init
{
    static void Prefix(TimeQTESettings handlerSettings)
    {
        if (handlerSettings == null) { Plugin.LogD("[TimeQTEHandler.Init] handlerSettings=null"); return; }
        Plugin.LogI(
            $"[TimeQTEHandler.Init] tooEarly={handlerSettings.TooEarlyInputWindow:F3} " +
            $"beforeOK={handlerSettings.BeforeQTEOkWindow:F3} " +
            $"perfect={handlerSettings.PerfectQTEWindow:F3} " +
            $"afterOK={handlerSettings.AfterQTEOKWindow:F3} " +
            $"override={handlerSettings.overrideSettings}");
    }
}

[HarmonyPatch(typeof(TimeQTESettings), "get_TooEarlyInputWindow")]
static class Log_TimeQTESettings_TooEarlyInputWindow
{
    static void Postfix(float __result)
        => Plugin.LogD($"[TimeQTESettings.TooEarlyInputWindow] -> {__result:F3}");
}

[HarmonyPatch(typeof(TimeQTESettings), "get_BeforeQTEOkWindow")]
static class Log_TimeQTESettings_BeforeQTEOkWindow
{
    static void Postfix(float __result)
        => Plugin.LogD($"[TimeQTESettings.BeforeQTEOkWindow] -> {__result:F3}");
}

[HarmonyPatch(typeof(TimeQTESettings), "get_PerfectQTEWindow")]
static class Log_TimeQTESettings_PerfectQTEWindow
{
    static void Postfix(float __result)
        => Plugin.LogD($"[TimeQTESettings.PerfectQTEWindow] -> {__result:F3}");
}

[HarmonyPatch(typeof(TimeQTESettings), "get_AfterQTEOKWindow")]
static class Log_TimeQTESettings_AfterQTEOKWindow
{
    static void Postfix(float __result)
        => Plugin.LogD($"[TimeQTESettings.AfterQTEOKWindow] -> {__result:F3}");
}

// \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
// DeflectMoonrangState  (Moongirl’s per-bounce deflect window)
// \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

/// <summary>EnableDeflect \u2014 the deflect window has opened; projectile is incoming.</summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.EnableDeflect))]
static class Log_DeflectMoonrangState_EnableDeflect
{
    static void Prefix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.EnableDeflect] PRE  | deflecting={__instance.Deflecting} deflectCount=?");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.EnableDeflect] POST | deflecting={__instance.Deflecting} \u2014 window open");
}

/// <summary>DisableDeflect \u2014 the deflect window has closed.</summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.DisableDeflect))]
static class Log_DeflectMoonrangState_DisableDeflect
{
    static void Prefix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.DisableDeflect] PRE  | deflecting={__instance.Deflecting}");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.DisableDeflect] POST | window closed");
}

/// <summary>
/// OnDeflectInput \u2014 fires when the player presses the button during a deflect window.
/// This is what normally sets deflecting=true in unmodded gameplay.
/// </summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.OnDeflectInput))]
static class Log_DeflectMoonrangState_OnDeflectInput
{
    static void Prefix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.OnDeflectInput] PRE  | deflecting={__instance.Deflecting}");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.OnDeflectInput] POST | deflecting={__instance.Deflecting}");
}

/// <summary>
/// OnDeflectProjectile \u2014 public method that sets deflecting=true.
/// Called by our Patch_DeflectMoonrangState_GetQTEResult prefix; should appear
/// exactly once per projectile arrival (from mod), and once more if the player
/// also pressed the button naturally.
/// </summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.OnDeflectProjectile))]
static class Log_DeflectMoonrangState_OnDeflectProjectile
{
    static void Prefix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.OnDeflectProjectile] PRE  | deflecting={__instance.Deflecting}");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.OnDeflectProjectile] POST | deflecting={__instance.Deflecting}");
}

/// <summary>
/// GetQTEResult \u2014 reads deflecting flag and writes result into teamQTEResult.
/// Logged at Info so the before/after deflecting state is always visible.
/// Signature: public void GetQTEResult(TeamQTEResult teamQTEResult)
/// </summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.GetQTEResult))]
static class Log_DeflectMoonrangState_GetQTEResult
{
    static void Prefix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.GetQTEResult] PRE  | deflecting={__instance.Deflecting}");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState.GetQTEResult] POST | deflecting={__instance.Deflecting}");
}


// \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
// AdditionalPlayersMoonrangDeflection  (co-op secondary deflectors)
// \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

/// <summary>
/// OnDeflectInput \u2014 co-op secondary player pressed during a deflect window.
/// Only fires in SinglePlayerPlus / co-op mode.
/// </summary>
[HarmonyPatch(typeof(AdditionalPlayersMoonrangDeflection), nameof(AdditionalPlayersMoonrangDeflection.OnDeflectInput))]
static class Log_AdditionalPlayersMoonrangDeflection_OnDeflectInput
{
    static void Prefix(AdditionalPlayersMoonrangDeflection __instance)
    {
        string playerName = __instance.Player != null ? __instance.Player.name : "null";
        Plugin.LogI(
            $"[AdditionalPlayersMoonrangDeflection.OnDeflectInput] PRE  | player={playerName} deflecting={__instance.Deflecting}");
    }

    static void Postfix(AdditionalPlayersMoonrangDeflection __instance)
        => Plugin.LogI(
            $"[AdditionalPlayersMoonrangDeflection.OnDeflectInput] POST | deflecting={__instance.Deflecting}");
}

// ──────────────────────────────────────────────────────────────────────
// Heal QTE pipeline  (PlayerSpecialMoveHealEffect + HitData)
//
// Theory: MendingLight (and other spell heals) call
//   timedAttackHandler.GetResult() -> CanAutoTimeHit (already patched) ->
//   EQTEResult.Perfect -> TeamQTEResult set on HitData ->
//   PlayerSpecialMoveHealEffect.GetInputModifier reads it -> boosted heal.
//
// These three patches let us verify each step in the log.
// ──────────────────────────────────────────────────────────────────────

/// <summary>
/// HitData.SetQTEResult -- called wherever the game attaches a TeamQTEResult to
/// a HitData (both damage and heal hits).  Logs the move name and whether the
/// attached result HasSuccess so we can confirm the heal HitData receives a
/// perfect result from our CanAutoTimeHit patch.
/// </summary>
[HarmonyPatch(typeof(HitData), nameof(HitData.SetQTEResult))]
static class Log_HitData_SetQTEResult
{
    static void Prefix(HitData __instance, TeamQTEResult __0)
    {
        string moveName = __instance.combatMove != null ? __instance.combatMove.name : "null";
        Plugin.LogI(
            $"[HitData.SetQTEResult] PRE  | move={moveName} result={((__0 != null) ? "non-null" : "null")}");
    }
}

/// <summary>
/// PlayerSpecialMoveHealEffect.GetInputModifier -- protected method that reads
/// hitData.QTEResult and returns the heal multiplier.
/// A value > 1.0 confirms the heal QTE result is Perfect / success; 1.0 means
/// no bonus (failed or missing QTE result).
/// </summary>
[HarmonyPatch(typeof(PlayerSpecialMoveHealEffect), "GetInputModifier")]
static class Log_PlayerSpecialMoveHealEffect_GetInputModifier
{
    static void Prefix(PlayerSpecialMoveHealEffect __instance, HitData __0)
    {
        string moveName = __0?.combatMove != null ? __0.combatMove.name : "null";
        Plugin.LogI(
            $"[HealEffect.GetInputModifier] PRE  | move={moveName} " +
            $"qteResult={((__0?.QTEResult != null) ? "non-null" : "null")} " +
            $"qteSuccessMult={__instance.qteSuccessMultiplier:F3} " +
            $"additionalBonus={__instance.additionalTimedHitQteSuccessMultiplierBonus:F3}");
    }

    static void Postfix(float __result)
        => Plugin.LogI(
            $"[HealEffect.GetInputModifier] POST | modifier={__result:F3}");
}

/// <summary>
/// PlayerSpecialMoveHealEffect.GetHealAmount -- final heal value returned to the
/// combat system.  Logging this alongside GetInputModifier lets us confirm the
/// two are consistent and the boosted multiplier is reflected in the final number.
/// </summary>
[HarmonyPatch(typeof(PlayerSpecialMoveHealEffect), nameof(PlayerSpecialMoveHealEffect.GetHealAmount))]
static class Log_PlayerSpecialMoveHealEffect_GetHealAmount
{
    static void Prefix(HitData __0)
    {
        string moveName = __0?.combatMove != null ? __0.combatMove.name : "null";
        Plugin.LogI(
            $"[HealEffect.GetHealAmount] PRE  | move={moveName}");
    }

    static void Postfix(int __result)
        => Plugin.LogI(
            $"[HealEffect.GetHealAmount] POST | healAmount={__result}");
}

/// <summary>
/// PercentageHeal.GetHealAmount -- alternative heal effect used by some moves
/// (e.g. CookerSurprise / Garl's Mega Gusto).  Also has a qteSuccessMultiplier
/// field, so logging it tells us whether this pathway fires and what it produces.
/// </summary>
[HarmonyPatch(typeof(PercentageHeal), nameof(PercentageHeal.GetHealAmount))]
static class Log_PercentageHeal_GetHealAmount
{
    static void Prefix(PercentageHeal __instance, HitData __0)
    {
        string moveName = __0?.combatMove != null ? __0.combatMove.name : "null";
        Plugin.LogI(
            $"[PercentageHeal.GetHealAmount] PRE  | move={moveName} " +
            $"percentage={__instance.percentage:F3} qteSuccessMult={__instance.qteSuccessMultiplier:F3} " +
            $"qteResult={((__0?.QTEResult != null) ? "non-null" : "null")}");
    }

    static void Postfix(int __result)
        => Plugin.LogI(
            $"[PercentageHeal.GetHealAmount] POST | healAmount={__result}");
}