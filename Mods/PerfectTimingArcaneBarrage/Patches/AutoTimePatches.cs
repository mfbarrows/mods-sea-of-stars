using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PerfectTimingArcaneBarrage.Patches;

// =============================================================================
//  AUTO-TIME PATCHES — PotionKick combo
//
//  Approach:
//    1. OnLobPotion Postfix  — each time a potion is lobbed, grab its Projectile
//       pointer and add it to _pendingPotions (our own tracked list).
//    2. KickPotionState.StateExecute Prefix  — runs every frame while Seraï is
//       in the kick state.  Iterates _pendingPotions and checks the distance
//       from each potion to kickImpactPosition.  If any potion is within
//       validKickMaxPotionDistance, invoke kickCallback (= PotionKick.OnKick)
//       and remove that potion from _pendingPotions so we kick it exactly once.
//       At most one potion is expected in range at a time; break after the first.
// =============================================================================

// Potions lobbed but not yet kicked — populated by OnLobPotion, consumed by StateExecute.
static class PotionKickState
{
    internal static readonly List<IntPtr> PendingPotions = new();
}

/// <summary>
/// Track each potion as it is lobbed so StateExecute can poll it every frame.
/// OnLobPotion fires synchronously after kickablePotions.Add(projectile), so
/// the Projectile pointer is valid immediately.
/// </summary>
[HarmonyPatch(typeof(PotionKick), "OnLobPotion")]
static class AutoTime_PotionKick_OnLobPotion
{
    static unsafe void Postfix(PotionKick __instance)
    {
        try
        {
            // private List<Projectile> kickablePotions;  // 0x198
            // The last entry is the potion just added by this OnLobPotion call.
            IntPtr listPtr = *(IntPtr*)((byte*)__instance.Pointer + 0x198);
            if (listPtr == IntPtr.Zero) return;
            var kickablePotions = new Il2CppSystem.Collections.Generic.List<Projectile>(listPtr);
            int count = kickablePotions.Count;
            if (count == 0) return;

            IntPtr potionPtr = kickablePotions[count - 1].Pointer;
            PotionKickState.PendingPotions.Add(potionPtr);
            Plugin.LogI($"[AutoTime] OnLobPotion | tracking potion {potionPtr:X} (pending={PotionKickState.PendingPotions.Count})");
        }
        catch (Exception ex)
        {
            Plugin.LogW($"[AutoTime] OnLobPotion | exception: {ex}");
        }
    }
}

/// <summary>
/// Every frame while Seraï is in the kick state, check each pending potion.
/// When one enters the valid kick zone, invoke kickCallback and remove it
/// so it is only kicked once.  Breaks after the first in-range potion since
/// at most one is expected in range at a time.
/// kickCallback @ 0x58 on KickPotionState → PotionKick.OnKick → UpdateKicks.
/// kickImpactPosition @ 0x178 on PotionKick (private Vector3).
/// </summary>
[HarmonyPatch(typeof(KickPotionState), "StateExecute")]
static class AutoTime_KickPotionState_StateExecute
{
    static unsafe void Prefix(KickPotionState __instance)
    {
        try
        {
            if (PotionKickState.PendingPotions.Count == 0) return;

            var kickCallback = __instance.kickCallback;
            if (kickCallback == null) return;
            var potionKick = kickCallback.Target as PotionKick;
            if (potionKick == null) return;

            // private Vector3 kickImpactPosition;  // 0x178
            Vector3 kickImpactPosition = *(Vector3*)((byte*)potionKick.Pointer + 0x178);
            float maxDist = potionKick.validKickMaxPotionDistance;

            for (int i = PotionKickState.PendingPotions.Count - 1; i >= 0; i--)
            {
                IntPtr ptr = PotionKickState.PendingPotions[i];
                var potion = new Projectile(ptr);
                if (potion == null) { PotionKickState.PendingPotions.RemoveAt(i); continue; }

                Vector3 delta = potion.transform.position - kickImpactPosition;
                float dist = Vector3.Magnitude(delta);

                Plugin.LogD($"[AutoTime] StateExecute | potion {ptr:X} dist={dist:F2} max={maxDist:F2}");

                if (dist > maxDist) continue;

                Plugin.LogI($"[AutoTime] StateExecute | potion {ptr:X} in range (dist={dist:F2}), invoking OnKickInput");
                PotionKickState.PendingPotions.RemoveAt(i);
                __instance.OnKickInput();
                break;  // at most one kick per frame
            }
        }
        catch (Exception ex)
        {
            Plugin.LogW($"[AutoTime] StateExecute | exception: {ex}");
        }
    }
}
