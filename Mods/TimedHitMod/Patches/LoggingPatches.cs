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
            $"[AbstractTimedAttackHandler] >> BeginInputPhase  | handler={__instance.GetType().Name} " +
            $"windowMult={__instance.WindowDurationMultiplier:F2}");

    static void Postfix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[AbstractTimedAttackHandler] << BeginInputPhase | handler={__instance.GetType().Name} -- window is now open");
}

/// <summary>EndInputPhase -- the QTE timing window has closed.</summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), nameof(AbstractTimedAttackHandler.EndInputPhase))]
static class Log_AttackHandler_EndInputPhase
{
    static void Prefix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[AbstractTimedAttackHandler] >> EndInputPhase  | handler={__instance.GetType().Name}");

    static void Postfix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[AbstractTimedAttackHandler] << EndInputPhase | handler={__instance.GetType().Name} -- window closed");
}

/// <summary>OnInputPressed -- the player pressed the button during an attack window.</summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), nameof(AbstractTimedAttackHandler.OnInputPressed))]
static class Log_AttackHandler_OnInputPressed
{
    static void Prefix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[AbstractTimedAttackHandler] >> OnInputPressed  | handler={__instance.GetType().Name} " +
            $"doQTEFeedback={__instance.doQTEResultFeedback}");

    static void Postfix(AbstractTimedAttackHandler __instance)
        => Plugin.LogI(
            $"[AbstractTimedAttackHandler] << OnInputPressed | handler={__instance.GetType().Name} -- press registered");
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
            $"[AbstractTimedAttackHandler] >> CanPressInput | handler={__instance.GetType().Name}");

    static void Postfix(AbstractTimedAttackHandler __instance, bool __result)
        => Plugin.LogD(
            $"[AbstractTimedAttackHandler] << CanPressInput | handler={__instance.GetType().Name} result={__result}");
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
            $"[TimedAttackHandler] >> GetResult | move={((__0 != null) ? __0.Pointer.ToString("X") : "null")}");

    static void Postfix(TimedAttackHandler __instance, PlayerCombatMoveDefinition __0)
        => Plugin.LogI(
            $"[TimedAttackHandler] << GetResult | move={((__0 != null) ? __0.Pointer.ToString("X") : "null")} -- QTE armed");
}

/// <summary>OnAttackResultReady -- fires once per-player with their individual QTE result.</summary>
[HarmonyPatch(typeof(TimedAttackHandler), "OnAttackResultReady")]
static class Log_TimedAttackHandler_OnAttackResultReady
{
    static void Prefix(TimedAttackHandler __instance, Rewired.Player __0, EQTEResult __1)
        => Plugin.LogI(
            $"[TimedAttackHandler] >> OnAttackResultReady | " +
            $"player={(__0 != null ? __0.Pointer.ToString("X") : "null")} result={__1}");

    static void Postfix(TimedAttackHandler __instance, Rewired.Player __0, EQTEResult __1)
        => Plugin.LogI(
            $"[TimedAttackHandler] << OnAttackResultReady | " +
            $"player={(__0 != null ? __0.Pointer.ToString("X") : "null")} result={__1} -- recorded");
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
    static void Postfix(AbstractTimedAttackHandler __instance, bool __result) {
        if (__result) {
            Plugin.LogD(
            $"[AbstractTimedAttackHandler] << CanAutoTimeHit | handler={__instance.GetType().Name} result={__result}");
        } else {
            Plugin.LogW(
            $"[AbstractTimedAttackHandler] << CanAutoTimeHit | handler={__instance.GetType().Name} result={__result}");
        }
        
    }
}

/// <summary>CanAutoTime -- called by CanAutoTimeHit for each active modifier to decide auto-timing.</summary>
[HarmonyPatch(typeof(AutoTimeAttackModifier), nameof(AutoTimeAttackModifier.CanAutoTime))]
static class Log_AutoTimeAttackModifier_CanAutoTime
{
    static void Prefix(AutoTimeAttackModifier __instance, PlayerCombatMoveDefinition __0)
        => Plugin.LogD(
            $"[AutoTimeAttackModifier] >> CanAutoTime  | " +
            $"chances={__instance.autoTimeChances:F2} move={((__0 != null) ? __0.Pointer.ToString("X") : "null")}");

    static void Postfix(AutoTimeAttackModifier __instance, bool __result) {
            if (__result) {
                Plugin.LogD(
                $"[AutoTimeAttackModifier] << CanAutoTime | chances={__instance.autoTimeChances:F2} result={__result}");
            } else {
                Plugin.LogW(
                $"[AutoTimeAttackModifier] << CanAutoTime | chances={__instance.autoTimeChances:F2} result={__result}");
            }
    }
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
            $"[AutoTimeBlockModifier] >> CanAutoTime | chances={__instance.autoTimeChances:F2}");

    static void Postfix(AutoTimeBlockModifier __instance, bool __result)
        => Plugin.LogD(
            $"[AutoTimeBlockModifier] << CanAutoTime | chances={__instance.autoTimeChances:F2} result={__result}");
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
            $"[TeamQTEResult] >> GetBestResult | qteId={__instance.QteId}");

    static void Postfix(QTEResult __result)
        => Plugin.LogD(
            $"[TeamQTEResult] << GetBestResult | best={__result.result}");
}

/// <summary>HasSuccess -- broad success gate used by many callers before reading the result.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.HasSuccess))]
static class Log_TeamQTEResult_HasSuccess
{
    static void Postfix(TeamQTEResult __instance, bool __result)
        => Plugin.LogD(
            $"[TeamQTEResult] << HasSuccess | qteId={__instance.QteId} result={__result}");

    // calling other patched methods from here causes re-entrant crashes.
}

/// <summary>GetSuccessCount -- used by multi-player checks (e.g. triple-block cancel).</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.GetSuccessCount))]
static class Log_TeamQTEResult_GetSuccessCount
{
    // static void Prefix(TeamQTEResult __instance)
    //     => Plugin.LogD(
    //         $"[TeamQTEResult] >> GetSuccessCount | qteId={__instance.QteId}");

    static void Postfix(TeamQTEResult __instance, int __result)
    {
        if (__result > 0) {
            Plugin.LogD(
            $"[TeamQTEResult] << GetSuccessCount | qteId={__instance.QteId} result={__result}");
        }
    }

}

/// <summary>GetResultForPlayer -- per-player result lookup used in co-op / individual feedback.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.GetResultForPlayer))]
static class Log_TeamQTEResult_GetResultForPlayer
{
    static void Prefix(TeamQTEResult __instance, Rewired.Player __0)
        => Plugin.LogD(
            $"[TeamQTEResult] >> GetResultForPlayer | qteId={__instance.QteId} " +
            $"player={(__0 != null ? __0.Pointer.ToString("X") : "null")}");

    // Postfix omitted -- QTEResult field access is safe but keeping symmetry with other simplifications.
}

/// <summary>HasSuccessForPlayer -- per-player success check.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.HasSuccessForPlayer))]
static class Log_TeamQTEResult_HasSuccessForPlayer
{
    static void Prefix(TeamQTEResult __instance, Rewired.Player __0)
        => Plugin.LogD(
            $"[TeamQTEResult] >> HasSuccessForPlayer | qteId={__instance.QteId} " +
            $"player={(__0 != null ? __0.Pointer.ToString("X") : "null")}");

    static void Postfix(TeamQTEResult __instance, Rewired.Player __0, bool __result)
        => Plugin.LogD(
            $"[TeamQTEResult] << HasSuccessForPlayer | qteId={__instance.QteId} " +
            $"player={(__0 != null ? __0.Pointer.ToString("X") : "null")} result={__result}");

    // Postfix omitted -- keeping patch symmetric with HasSuccess simplification.
}

/// <summary>AddResult -- records one player's QTE result into the team result; called once per player
/// per accepted kick event inside PotionKick.UpdateKickForPlayer.</summary>
[HarmonyPatch(typeof(TeamQTEResult), nameof(TeamQTEResult.AddResult))]
static class Log_TeamQTEResult_AddResult
{
    static void Prefix(TeamQTEResult __instance, QTEResult __0)
        => Plugin.LogI(
            $"[TeamQTEResult] >> AddResult | qteId={__instance.QteId} " +
            $"player={(__0.owner != null ? __0.owner.Pointer.ToString("X") : "null")} " +
            $"result={__0.result}");

    static void Postfix(TeamQTEResult __instance)
        => Plugin.LogI(
            $"[TeamQTEResult] << AddResult | qteId={__instance.QteId}");
}


// ──────────────────────────────────────────────────────────────────────
// Block damage dispatch chain  (CombatDamage -> modifiers)
// ──────────────────────────────────────────────────────────────────────

/// <summary>ApplyBlockSuccessModifier -- applies damage cap and success bonuses when block succeeded.</summary>
[HarmonyPatch(typeof(CombatDamage), "ApplyBlockSuccessModifier")]
static class Log_CombatDamage_ApplyBlockSuccessModifier
{
    static void Prefix()
        => Plugin.LogI("[CombatDamage] >> ApplyBlockSuccessModifier");

    static void Postfix()
        => Plugin.LogI("[CombatDamage] << ApplyBlockSuccessModifier");
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
            $"[PlayerCombatActor] >> OnBlockInput | actor={__instance.GetType().Name}");

    static void Postfix(PlayerCombatActor __instance)
        => Plugin.LogI(
            $"[PlayerCombatActor] << OnBlockInput | actor={__instance.GetType().Name}");
}

/// <summary>PlayerCombatActor.GetBlockResult -- retrieves the block QTE result from the actor.</summary>
[HarmonyPatch(typeof(PlayerCombatActor), nameof(PlayerCombatActor.GetBlockResult))]
static class Log_PlayerCombatActor_GetBlockResult
{
    static void Prefix(PlayerCombatActor __instance)
        => Plugin.LogI(
            $"[PlayerCombatActor] >> GetBlockResult | actor={__instance.GetType().Name}");

    static void Postfix(TeamQTEResult __result)
        => Plugin.LogI(
            $"[PlayerCombatActor] << GetBlockResult | result={((__result != null) ? "non-null" : "null")}");
}

/// <summary>TimedBlockFailedDamagePercentageModifier -- extra damage penalty on failed block.</summary>
[HarmonyPatch(typeof(TimedBlockFailedDamagePercentageModifier), nameof(TimedBlockFailedDamagePercentageModifier.GetModifiedDamage))]
static class Log_TimedBlockFailedDamage_GetModifiedDamage
{
    static void Prefix(float __0)
        => Plugin.LogI(
            $"[TimedBlockFailedDamage] >> GetModifiedDamage | baseDamage={__0:F1} -- should not appear with mod active");

    static void Postfix(float __result)
        => Plugin.LogI(
            $"[TimedBlockFailedDamage] << GetModifiedDamage | modifiedDamage={__result:F1}");
}

/// <summary>TimedBlockAbsoluteDamageCapModifier -- caps damage on a successful block.</summary>
[HarmonyPatch(typeof(TimedBlockAbsoluteDamageCapModifier), nameof(TimedBlockAbsoluteDamageCapModifier.GetModifiedDamage))]
static class Log_TimedBlockDamageCap_GetModifiedDamage
{
    static void Prefix(float __0)
        => Plugin.LogI(
            $"[TimedBlockDamageCap] >> GetModifiedDamage | baseDamage={__0:F1}");

    static void Postfix(float __result)
        => Plugin.LogI(
            $"[TimedBlockDamageCap] << GetModifiedDamage | modifiedDamage={__result:F1}");
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
            $"[TimeQTEHandler] >> Init | tooEarly={handlerSettings.TooEarlyInputWindow:F3} " +
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
        => Plugin.LogD($"[TimeQTESettings] << TooEarlyInputWindow | result={__result:F3}");
}

[HarmonyPatch(typeof(TimeQTESettings), "get_BeforeQTEOkWindow")]
static class Log_TimeQTESettings_BeforeQTEOkWindow
{
    static void Postfix(float __result)
        => Plugin.LogD($"[TimeQTESettings] << BeforeQTEOkWindow | result={__result:F3}");
}

[HarmonyPatch(typeof(TimeQTESettings), "get_PerfectQTEWindow")]
static class Log_TimeQTESettings_PerfectQTEWindow
{
    static void Postfix(float __result)
        => Plugin.LogD($"[TimeQTESettings] << PerfectQTEWindow | result={__result:F3}");
}

[HarmonyPatch(typeof(TimeQTESettings), "get_AfterQTEOKWindow")]
static class Log_TimeQTESettings_AfterQTEOKWindow
{
    static void Postfix(float __result)
        => Plugin.LogD($"[TimeQTESettings] << AfterQTEOKWindow | result={__result:F3}");
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
            $"[DeflectMoonrangState] >> EnableDeflect | deflecting={__instance.Deflecting} deflectCount=?");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState] << EnableDeflect | deflecting={__instance.Deflecting} \u2014 window open");
}

/// <summary>DisableDeflect \u2014 the deflect window has closed.</summary>
[HarmonyPatch(typeof(DeflectMoonrangState), nameof(DeflectMoonrangState.DisableDeflect))]
static class Log_DeflectMoonrangState_DisableDeflect
{
    static void Prefix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState] >> DisableDeflect | deflecting={__instance.Deflecting}");
    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState] << DisableDeflect | window closed");
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
            $"[DeflectMoonrangState] >> OnDeflectInput | deflecting={__instance.Deflecting}");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState] << OnDeflectInput | deflecting={__instance.Deflecting}");
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
            $"[DeflectMoonrangState] >> OnDeflectProjectile | deflecting={__instance.Deflecting}");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState] << OnDeflectProjectile | deflecting={__instance.Deflecting}");
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
            $"[DeflectMoonrangState] >> GetQTEResult | deflecting={__instance.Deflecting}");

    static void Postfix(DeflectMoonrangState __instance)
        => Plugin.LogI(
            $"[DeflectMoonrangState] << GetQTEResult | deflecting={__instance.Deflecting}");
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
            $"[AdditionalPlayersMoonrangDeflection] >> OnDeflectInput | player={playerName} deflecting={__instance.Deflecting}");
    }

    static void Postfix(AdditionalPlayersMoonrangDeflection __instance)
        => Plugin.LogI(
            $"[AdditionalPlayersMoonrangDeflection] << OnDeflectInput | deflecting={__instance.Deflecting}");
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
        string moveName = __instance.combatMove != null ? __instance.combatMove.Pointer.ToString("X") : "null";
        Plugin.LogI(
            $"[HitData] >> SetQTEResult | move={moveName} result={((__0 != null) ? "non-null" : "null")}");
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
        string moveName = __0?.combatMove != null ? __0.combatMove.Pointer.ToString("X") : "null";
        Plugin.LogI(
            $"[PlayerSpecialMoveHealEffect] >> GetInputModifier | move={moveName} " +
            $"qteResult={((__0?.QTEResult != null) ? "non-null" : "null")} " +
            $"qteSuccessMult={__instance.qteSuccessMultiplier:F3} " +
            $"additionalBonus={__instance.additionalTimedHitQteSuccessMultiplierBonus:F3}");
    }

    static void Postfix(float __result)
        => Plugin.LogI(
            $"[PlayerSpecialMoveHealEffect] << GetInputModifier | modifier={__result:F3}");
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
        string moveName = __0?.combatMove != null ? __0.combatMove.Pointer.ToString("X") : "null";
        Plugin.LogI(
            $"[PlayerSpecialMoveHealEffect] >> GetHealAmount | move={moveName}");
    }

    static void Postfix(int __result)
        => Plugin.LogI(
            $"[PlayerSpecialMoveHealEffect] << GetHealAmount | healAmount={__result}");
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
        string moveName = __0?.combatMove != null ? __0.combatMove.Pointer.ToString("X") : "null";
        Plugin.LogI(
            $"[PercentageHeal] >> GetHealAmount | move={moveName} " +
            $"percentage={__instance.percentage:F3} qteSuccessMult={__instance.qteSuccessMultiplier:F3} " +
            $"qteResult={((__0?.QTEResult != null) ? "non-null" : "null")}");
    }

    static void Postfix(int __result)
        => Plugin.LogI(
            $"[PercentageHeal] << GetHealAmount | healAmount={__result}");
}


// // ──────────────────────────────────────────────────────────────────────
// // SunboyShootQTESunballState  (Solar Rain / Soonrang sunball QTE)
// // Every method logged so we can trace the exact call sequence.
// // ──────────────────────────────────────────────────────────────────────

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "StateEnter")]
// static class Log_SunboyQTE_StateEnter
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> StateEnter");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << StateEnter");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "StateExit")]
// static class Log_SunboyQTE_StateExit
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> StateExit");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << StateExit");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "StateExecute")]
// static class Log_SunboyQTE_StateExecute
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> StateExecute");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << StateExecute");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "BeginDisplayInstructions")]
// static class Log_SunboyQTE_BeginDisplayInstructions
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> BeginDisplayInstructions");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << BeginDisplayInstructions");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "BeginIn")]
// static class Log_SunboyQTE_BeginIn
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> BeginIn");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << BeginIn");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateIn")]
// static class Log_SunboyQTE_UpdateIn
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> UpdateIn");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << UpdateIn");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "BeginCharge")]
// static class Log_SunboyQTE_BeginCharge
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> BeginCharge");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << BeginCharge");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "SpawnSunball")]
// static class Log_SunboyQTE_SpawnSunball
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> SpawnSunball");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << SpawnSunball");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateCharging")]
// static class Log_SunboyQTE_UpdateCharging
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> UpdateCharging");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << UpdateCharging");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateSunballCharge")]
// static class Log_SunboyQTE_UpdateSunballCharge
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> UpdateSunballCharge");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << UpdateSunballCharge");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "OnReachedMaxCharge")]
// static class Log_SunboyQTE_OnReachedMaxCharge
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> OnReachedMaxCharge");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << OnReachedMaxCharge");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "OnWentPastMaxCharge")]
// static class Log_SunboyQTE_OnWentPastMaxCharge
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> OnWentPastMaxCharge");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << OnWentPastMaxCharge");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "BeginShoot")]
// static class Log_SunboyQTE_BeginShoot
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> BeginShoot");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << BeginShoot");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "ThrowSunball")]
// static class Log_SunboyQTE_ThrowSunball
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> ThrowSunball");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << ThrowSunball");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "OnShootSunball")]
// static class Log_SunboyQTE_OnShootSunball
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> OnShootSunball");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << OnShootSunball");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "HasQTESuccess")]
// static class Log_SunboyQTE_HasQTESuccess
// {
//     static void Prefix()              => Plugin.LogD("[QTESunballState] >> HasQTESuccess");
//     static void Postfix(bool __result) => Plugin.LogD($"[QTESunballState] << HasQTESuccess -> {__result}");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "HasAdditionalPlayers")]
// static class Log_SunboyQTE_HasAdditionalPlayers
// {
//     static void Prefix()              => Plugin.LogD("[QTESunballState] >> HasAdditionalPlayers");
//     static void Postfix(bool __result) => Plugin.LogD($"[QTESunballState] << HasAdditionalPlayers -> {__result}");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "IsWaitingForAdditionalPlayers")]
// static class Log_SunboyQTE_IsWaitingForAdditionalPlayers
// {
//     static void Prefix()              => Plugin.LogD("[QTESunballState] >> IsWaitingForAdditionalPlayers");
//     static void Postfix(bool __result) => Plugin.LogD($"[QTESunballState] << IsWaitingForAdditionalPlayers -> {__result}");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "GatherAdditionalPlayers")]
// static class Log_SunboyQTE_GatherAdditionalPlayers
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> GatherAdditionalPlayers");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << GatherAdditionalPlayers");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateAdditionalPlayers")]
// static class Log_SunboyQTE_UpdateAdditionalPlayers
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> UpdateAdditionalPlayers");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << UpdateAdditionalPlayers");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "DoAdditionalPlayersQTEFeedback")]
// static class Log_SunboyQTE_DoAdditionalPlayersQTEFeedback
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> DoAdditionalPlayersQTEFeedback");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << DoAdditionalPlayersQTEFeedback");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateEndPause")]
// static class Log_SunboyQTE_UpdateEndPause
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> UpdateEndPause");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << UpdateEndPause");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "DoRecoil")]
// static class Log_SunboyQTE_DoRecoil
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> DoRecoil");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << DoRecoil");
// }

// [HarmonyPatch(typeof(SunboyShootQTESunballState), "UpdateRecoil")]
// static class Log_SunboyQTE_UpdateRecoil
// {
//     static void Prefix()  => Plugin.LogD("[QTESunballState] >> UpdateRecoil");
//     static void Postfix() => Plugin.LogD("[QTESunballState] << UpdateRecoil");
// }


// // ──────────────────────────────────────────────────────────────────────
// // SunboyShootSunballState  (non-QTE sunball shoot state)
// // ──────────────────────────────────────────────────────────────────────

// [HarmonyPatch(typeof(SunboyShootSunballState), "StateEnter")]
// static class Log_ShootSunball_StateEnter
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> StateEnter");
//     static void Postfix() => Plugin.LogD("[SunballState] << StateEnter");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "StateExecute")]
// static class Log_ShootSunball_StateExecute
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> StateExecute");
//     static void Postfix() => Plugin.LogD("[SunballState] << StateExecute");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "StateExit")]
// static class Log_ShootSunball_StateExit
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> StateExit");
//     static void Postfix() => Plugin.LogD("[SunballState] << StateExit");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "OnSpawnSunball")]
// static class Log_ShootSunball_OnSpawnSunball
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> OnSpawnSunball");
//     static void Postfix() => Plugin.LogD("[SunballState] << OnSpawnSunball");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "UpdateSunballCharge")]
// static class Log_ShootSunball_UpdateSunballCharge
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> UpdateSunballCharge");
//     static void Postfix() => Plugin.LogD("[SunballState] << UpdateSunballCharge");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), nameof(SunboyShootSunballState.ThrowSunball))]
// static class Log_ShootSunball_ThrowSunball
// {
//     static void Prefix(float normalizedTime)  => Plugin.LogD($"[SunballState] >> ThrowSunball normalizedTime={normalizedTime:F3}");
//     static void Postfix()                     => Plugin.LogD("[SunballState] << ThrowSunball");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "OnShootSunball")]
// static class Log_ShootSunball_OnShootSunball
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> OnShootSunball");
//     static void Postfix() => Plugin.LogD("[SunballState] << OnShootSunball");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "DoRecoil")]
// static class Log_ShootSunball_DoRecoil
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> DoRecoil");
//     static void Postfix() => Plugin.LogD("[SunballState] << DoRecoil");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "UpdateRecoil")]
// static class Log_ShootSunball_UpdateRecoil
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> UpdateRecoil");
//     static void Postfix() => Plugin.LogD("[SunballState] << UpdateRecoil");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "SetDefaultValues")]
// static class Log_ShootSunball_SetDefaultValues
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> SetDefaultValues");
//     static void Postfix() => Plugin.LogD("[SunballState] << SetDefaultValues");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), nameof(SunboyShootSunballState.GetTimeAfterShot))]
// static class Log_ShootSunball_GetTimeAfterShot
// {
//     static void Prefix()           => Plugin.LogD("[SunballState] >> GetTimeAfterShot");
//     static void Postfix(float __result) => Plugin.LogD($"[SunballState] << GetTimeAfterShot -> {__result:F3}");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), nameof(SunboyShootSunballState.GetRecoilEndPosition))]
// static class Log_ShootSunball_GetRecoilEndPosition
// {
//     static void Prefix()  => Plugin.LogD("[SunballState] >> GetRecoilEndPosition");
//     static void Postfix() => Plugin.LogD("[SunballState] << GetRecoilEndPosition");
// }

// [HarmonyPatch(typeof(SunboyShootSunballState), "get_ReadyToShoot")]
// static class Log_ShootSunball_ReadyToShoot
// {
//     static void Postfix(bool __result) => Plugin.LogD($"[SunballState] << ReadyToShoot -> {__result}");
// }


// // ──────────────────────────────────────────────────────────────────────
// // SunballProjectile  (projectile level tracking)
// // ──────────────────────────────────────────────────────────────────────

// [HarmonyPatch(typeof(SunballProjectile), nameof(SunballProjectile.IncreaseLevel))]
// static class Log_SunballProjectile_IncreaseLevel
// {
//     static void Prefix(SunballProjectile __instance)
//         => Plugin.LogD($"[SunballProjectile] >> IncreaseLevel");

//     static void Postfix(SunballProjectile __instance)
//         => Plugin.LogD($"[SunballProjectile] << IncreaseLevel");
// }

// [HarmonyPatch(typeof(SunballProjectile), nameof(SunballProjectile.DecreaseLevel))]
// static class Log_SunballProjectile_DecreaseLevel
// {
//     static void Prefix(SunballProjectile __instance)
//         => Plugin.LogD($"[SunballProjectile] >> DecreaseLevel");

//     static void Postfix(SunballProjectile __instance)
//         => Plugin.LogD($"[SunballProjectile] << DecreaseLevel");
// }

// [HarmonyPatch(typeof(SunballProjectile), "SetLevel")]
// static class Log_SunballProjectile_SetLevel
// {
//     static void Prefix(SunballProjectile __instance, int level)
//         => Plugin.LogD($"[SunballProjectile] >> SetLevel | current={__instance.Level} new={level}");

//     static void Postfix(SunballProjectile __instance)
//         => Plugin.LogD($"[SunballProjectile] << SetLevel | level={__instance.Level}");


// ──────────────────────────────────────────────────────────────────────
// SeraiReshanComboAddCastingTimeEffect  (Arcane Barrage / ArcaneMoons combo)
//
// This CombatEffect adds casting time to Resh'an's timed inputs during the
// Seraï+Resh'an team combo.
//
// PrepareData(HitData hitData) -- called once per hit to capture the hitData
//   and derive the timeToAdd value from timeToAddFirstHit /
//   timeToAddPerAdditionalHit before the hit resolves.
//
// OnDataReady() -- called after PrepareData; at this point dataReady==true
//   and the effect applies the casting-time addition to the hit.
//
// Logging goal: confirm these methods fire during Arcane Barrage, understand
// how many times they are called (once per hit?), and read the final timeToAdd
// so we can decide where to inject auto-timing.
//
// SeraiReshanComboAddCastingTimeEffect fields:
//   public int   timeToAddFirstHit;         // 0x48
//   public float timeToAddPerAdditionalHit; // 0x4C
//   private int  timeToAdd;                 // 0x50  (read via unsafe ptr below)
// ──────────────────────────────────────────────────────────────────────

/// <summary>
/// PrepareData -- first of the two override entry-points; receives the HitData
/// for the incoming hit.  Logs the move name, hit index flags, and the two
/// public configuration fields so we can see what the effect is configured with.
/// </summary>
[HarmonyPatch(typeof(SeraiReshanComboAddCastingTimeEffect), "PrepareData")]
static class Log_SeraiReshanCombo_PrepareData
{
    static void Prefix(SeraiReshanComboAddCastingTimeEffect __instance, HitData __0)
    {
        string moveName = __0?.combatMove != null ? __0.combatMove.Pointer.ToString("X") : "null";
        Plugin.LogI(
            $"[SeraiReshanCombo.PrepareData] PRE  | " +
            $"move={moveName} " +
            $"finalHit={(__0 != null ? __0.finalHit.ToString() : "?")} " +
            $"damage={(__0 != null ? __0.damage.ToString() : "?")} " +
            $"timeToAddFirstHit={__instance.timeToAddFirstHit} " +
            $"timeToAddPerAdditionalHit={__instance.timeToAddPerAdditionalHit:F3}");
    }

    static void Postfix(SeraiReshanComboAddCastingTimeEffect __instance, HitData __0)
    {
        string moveName = __0?.combatMove != null ? __0.combatMove.Pointer.ToString("X") : "null";
        Plugin.LogI(
            $"[SeraiReshanCombo.PrepareData] POST | move={moveName} -- data prepared");
    }
}

/// <summary>
/// OnDataReady -- fires after PrepareData; the effect is about to be applied.
/// Reads the private timeToAdd field (offset 0x50) via unsafe pointer to see
/// what value was computed from the hit count.
/// </summary>
[HarmonyPatch(typeof(SeraiReshanComboAddCastingTimeEffect), "OnDataReady")]
static class Log_SeraiReshanCombo_OnDataReady
{
    static unsafe void Prefix(SeraiReshanComboAddCastingTimeEffect __instance)
    {
        int timeToAdd = *(int*)((byte*)__instance.Pointer + 0x50);
        Plugin.LogI(
            $"[SeraiReshanCombo.OnDataReady] PRE  | " +
            $"timeToAddFirstHit={__instance.timeToAddFirstHit} " +
            $"timeToAddPerAdditionalHit={__instance.timeToAddPerAdditionalHit:F3} " +
            $"timeToAdd(0x50)={timeToAdd}");
    }

    static unsafe void Postfix(SeraiReshanComboAddCastingTimeEffect __instance)
    {
        int timeToAdd = *(int*)((byte*)__instance.Pointer + 0x50);
        Plugin.LogI(
            $"[SeraiReshanCombo.OnDataReady] POST | " +
            $"timeToAdd(0x50)={timeToAdd} -- effect applied");
    }
}


// ──────────────────────────────────────────────────────────────────────
// HitData  (additional accessors)
// ──────────────────────────────────────────────────────────────────────

/// WARNING: this is called extremely frequently, including out of combat
// [HarmonyPatch(typeof(HitData), "get_QTEResult")]
// static class Log_HitData_GetQTEResult
// {
//     static void Prefix(HitData __instance)
//     {
//         Plugin.LogD(
//             $"[HitData] >> get_QTEResult | data={__instance}");
//     }

//     static void Postfix(TeamQTEResult __result)
//         => Plugin.LogD(
//             $"[HitData] << get_QTEResult | result={__result}");
// }


// ──────────────────────────────────────────────────────────────────────
// MultiHitHandler.HitTarget  (all three overloads)
//
// Overload 1: HitTarget(CombatActor attacker, CombatTarget target, TeamQTEResult qteResult)
//   -- top-level entry point used by moves that supply a QTE result directly.
// Overload 2: HitTarget(HitData hitData)
//   -- public single-argument form; wraps private overload with poolHitData=false.
// Overload 3: HitTarget(HitData hitData, bool poolHitData)
//   -- private; where the actual dispatch happens.
// ──────────────────────────────────────────────────────────────────────

/// <summary>HitTarget(CombatActor, CombatTarget, TeamQTEResult) -- top-level QTE overload.</summary>
[HarmonyPatch(typeof(MultiHitHandler), "HitTarget",
    new System.Type[] { typeof(CombatActor), typeof(CombatTarget), typeof(TeamQTEResult) })]
static class Log_MultiHitHandler_HitTarget_QTE
{
    static void Prefix(CombatActor __0, CombatTarget __1, TeamQTEResult __2)
        => Plugin.LogI(
            $"[MultiHitHandler] >> HitTarget(attacker, target, qte) | " +
            $"actor={__0} " +
            $"target={__1} " +
            $"qte={__2}");

    static void Postfix()
        => Plugin.LogI("[MultiHitHandler] << HitTarget(attacker, target, qte)");
}

/// <summary>HitTarget(HitData) -- public single-HitData overload.</summary>
[HarmonyPatch(typeof(MultiHitHandler), "HitTarget",
    new System.Type[] { typeof(HitData) })]
static class Log_MultiHitHandler_HitTarget_HitData
{
    static void Prefix(HitData __0)
    {
        Plugin.LogI(
            $"[MultiHitHandler] >> HitTarget(hitData) | data={__0}");
    }

    static void Postfix()
        => Plugin.LogI("[MultiHitHandler] << HitTarget(hitData)");
}

/// <summary>HitTarget(HitData, bool) -- private core overload; poolHitData controls HitData lifetime.</summary>
[HarmonyPatch(typeof(MultiHitHandler), "HitTarget",
    new System.Type[] { typeof(HitData), typeof(bool) })]
static class Log_MultiHitHandler_HitTarget_HitData_Pool
{
    static void Prefix(HitData __0, bool __1)
    {
        Plugin.LogI(
            $"[MultiHitHandler] >> HitTarget(hitData, poolHitData) | data={__0} " +
            $"pool={__1}");
    }

    static void Postfix()
        => Plugin.LogI("[MultiHitHandler] << HitTarget(hitData, poolHitData)");
}


// ──────────────────────────────────────────────────────────────────────
// SinglePlayerPlusAttack  (co-op secondary attacker QTE handler)
// ──────────────────────────────────────────────────────────────────────

/// WARNING: this gets called 12-13 thousand times per second, starting at game startup
// [HarmonyPatch(typeof(SinglePlayerPlusAttack), "OnAttackResultReady")]
// static class Log_SinglePlayerPlusAttack_OnAttackResultReady
// {
//     private static double _lastLogTime = -1.0;
//     private static int _skipped = 0;

//     static void Prefix(Rewired.Player __0, EQTEResult __1)
//     {
//         double now = UnityEngine.Time.timeAsDouble;
//         if (now - _lastLogTime < 1.0) { _skipped++; return; }
//         int skipped = _skipped;
//         _skipped = 0;
//         _lastLogTime = now;
//         Plugin.LogD(
//             $"[SinglePlayerPlusAttack] >> OnAttackResultReady | " +
//             $"player={(__0 != null ? __0.Pointer.ToString("X") : "null")} result={__1} " +
//             $"(+{skipped} since last log)");
//     }
// }

/// <summary>OnSelfAttackResultReady -- fires when the local player's own result is ready.</summary>
[HarmonyPatch(typeof(SinglePlayerPlusAttack), "OnSelfAttackResultReady")]
static class Log_SinglePlayerPlusAttack_OnSelfAttackResultReady
{
    static void Prefix(EQTEResult __0)
        => Plugin.LogI(
            $"[SinglePlayerPlusAttack] >> OnSelfAttackResultReady | result={__0}");

    static void Postfix(EQTEResult __0)
        => Plugin.LogI(
            $"[SinglePlayerPlusAttack] << OnSelfAttackResultReady | result={__0}");
}

[HarmonyPatch(typeof(InputCategory), "GetButton")]
static class Log_InputCategory_GetButton
{
    static void Postfix(InputCategory __instance, string button, ref bool __result)
    {
        if (__result) {
            Plugin.LogD($"[InputCategory] << GetButton |category={__instance} button={button} result={__result}");
        }
    }
}

// ──────────────────────────────────────────────────────────────────────
// PlayerCombatMove.ShowInstructions  (instruction text display coroutine)
// ──────────────────────────────────────────────────────────────────────

/// <summary>
/// ShowInstructions -- protected coroutine on PlayerCombatMove; starts the
/// instruction-text typewriter display before a combo QTE begins.
/// Patch fires when the coroutine object is created (i.e. at StartCoroutine
/// call time), not once per MoveNext tick.
/// </summary>
[HarmonyPatch(typeof(PlayerCombatMove), "ShowInstructions")]
static class Log_PlayerCombatMove_ShowInstructions
{
    static void Prefix(PlayerCombatMove __instance)
        => Plugin.LogI(
            $"[PlayerCombatMove] >> ShowInstructions | move={__instance.GetType().Name}");

    static void Postfix(PlayerCombatMove __instance)
        => Plugin.LogI(
            $"[PlayerCombatMove] << ShowInstructions | move={__instance.GetType().Name} -- coroutine created");
}
// }