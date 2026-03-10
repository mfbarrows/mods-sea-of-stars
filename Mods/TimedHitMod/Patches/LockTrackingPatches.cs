using System;
using System.Collections.Generic;
using HarmonyLib;

namespace TimedHitMod.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// LockTracker  –  "set of enemies pending a hit" approach
//
// For both Moonrang and FanOfKnives we maintain a HashSet<IntPtr>:
//   EnemiesPendingMoonrangHit / EnemiesPendingFanOfKnivesHit
//
// Lifecycle
//   Init  (move start, first RefillAvailableTargetsLeft):
//     Add ALL enemies in the target list, regardless of locks.
//
//   Hit landed (HitData.SetQTEResult, filtered by move name):
//     Remove the hit enemy from the set.
//     OnLocksChanged fires immediately after if a lock broke.
//
//   OnLocksChanged (lock state changed on an enemy):
//     If the enemy still has matching locks → re-add to the set
//     (they need another hit to break the remaining lock).
//     If no matching locks remain → leave them removed.
//
//   Grant auto-time / deflect:
//     While the set is non-empty.
//     Force stop (result = false / return) when empty.
//
// This naturally covers both the initial "hit every enemy once" pass and the
// "keep hitting enemies that still have locks" extension, without any separate
// counter-based limit or WaitingForLockBreak gate.
// ─────────────────────────────────────────────────────────────────────────────

static class LockTracker
{
    internal static readonly HashSet<IntPtr> EnemiesPendingMoonrangHit    = new();
    internal static readonly HashSet<IntPtr> EnemiesPendingFanOfKnivesHit = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    internal static bool EnemyHasMatchingLocks(
        EnemyCombatActor enemy,
        Il2CppSystem.Collections.Generic.List<EDamageType> moveTypes)
    {
        if (moveTypes == null || moveTypes.Count == 0) return false;
        var spellLocks = enemy?.castingData?.spellLocks;
        if (spellLocks == null || spellLocks.Count == 0) return false;

        for (int i = 0; i < spellLocks.Count; i++)
        {
            var dt = spellLocks[i].damageType;
            if (dt == null) continue;
            for (int j = 0; j < moveTypes.Count; j++)
                if ((dt.damageType & moveTypes[j]) != 0)
                    return true;
        }
        return false;
    }

    // ── Initialisation ───────────────────────────────────────────────────────

    /// <summary>
    /// Clears <paramref name="set"/> and adds every enemy in the target list.
    /// Called once at move start (first RefillAvailableTargetsLeft).
    /// </summary>
    internal static void InitAllTargets(
        Il2CppSystem.Collections.Generic.List<CombatTarget> targets,
        HashSet<IntPtr> set)
    {
        set.Clear();
        if (targets == null) return;
        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var tgt = targets[i];
                if (tgt?.owner == null) continue;
                var enemy = tgt.owner.TryCast<EnemyCombatActor>();
                if (enemy != null)
                    set.Add(enemy.Pointer);
            }
            Plugin.LogI(
                $"[LockTracker] Init: {set.Count}/{targets.Count} enemies added to pending set");
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[LockTracker] InitAllTargets ERROR: {ex.Message}");
        }
    }

    // ── Per-hit update (called from HitData.SetQTEResult) ────────────────────

    /// <summary>
    /// Remove the hit enemy from the appropriate pending set.
    /// OnLocksChanged will re-add them if they still have matching locks.
    /// </summary>
    internal static void OnHitApplied(HitData hitData, string moveName)
    {
        try
        {
            var enemy = hitData.target?.owner?.TryCast<EnemyCombatActor>();
            if (enemy == null) return;
            IntPtr ptr = enemy.Pointer;

            if (moveName.Contains("Moonrang"))
            {
                EnemiesPendingMoonrangHit.Remove(ptr);
                Plugin.LogI(
                    $"[LockTracker] Moonrang hit 0x{ptr:X} → removed (pending={EnemiesPendingMoonrangHit.Count})");
            }
            else if (moveName.Contains("Soonrang"))
            {
                EnemiesPendingMoonrangHit.Remove(ptr);
                Plugin.LogI(
                    $"[LockTracker] Soonrang hit 0x{ptr:X} → removed (pending={EnemiesPendingMoonrangHit.Count})");
            }
            else if (moveName.Contains("SeraiFanOfKnives"))
            {
                EnemiesPendingFanOfKnivesHit.Remove(ptr);
                Plugin.LogI(
                    $"[LockTracker] FanOfKnives hit 0x{ptr:X} → removed (pending={EnemiesPendingFanOfKnivesHit.Count})");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[LockTracker] OnHitApplied ERROR: {ex.Message}");
        }
    }

    // ── Per-lock-change update (called from OnLocksChanged) ──────────────────

    /// <summary>
    /// Re-adds the enemy if they still have matching locks — meaning they need
    /// another hit. If no locks remain they are already gone from the set and
    /// stay gone.
    /// </summary>
    internal static void UpdateForEnemy(EnemyCombatActor enemy)
    {
        try
        {
            IntPtr ptr = enemy.Pointer;
            bool hasMoonrangLocks    = EnemyHasMatchingLocks(enemy, MoonrangCycleFlag.MoveDamageTypes);
            bool hasFokLocks         = EnemyHasMatchingLocks(enemy, FanOfKnivesCycleFlag.MoveDamageTypes);

            if (hasMoonrangLocks)    EnemiesPendingMoonrangHit.Add(ptr);
            if (hasFokLocks)         EnemiesPendingFanOfKnivesHit.Add(ptr);

            Plugin.LogI(
                $"[LockTracker] OnLocksChanged 0x{ptr:X}: " +
                $"moonrangLocks={hasMoonrangLocks} fokLocks={hasFokLocks} | " +
                $"MoonPending={EnemiesPendingMoonrangHit.Count} " +
                $"FokPending={EnemiesPendingFanOfKnivesHit.Count}");
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[LockTracker] UpdateForEnemy ERROR: {ex.Message}");
        }
    }
}

// ── Patches ──────────────────────────────────────────────────────────────────

/// <summary>
/// Fires after the engine updates an enemy's spell locks.
/// Re-adds the enemy to the pending sets if they still have matching locks.
/// </summary>
[HarmonyPatch(typeof(EnemyCombatActor), nameof(EnemyCombatActor.OnLocksChanged))]
static class Patch_EnemyCombatActor_OnLocksChanged
{
    static void Postfix(EnemyCombatActor __instance)
        => LockTracker.UpdateForEnemy(__instance);
}

/// <summary>
/// Fires when a hit is applied to a target with a QTE result attached.
/// Removes the hit enemy from the appropriate pending set; OnLocksChanged
/// will re-add them if they still have locks remaining.
/// </summary>
[HarmonyPatch(typeof(HitData), nameof(HitData.SetQTEResult))]
static class Patch_HitData_SetQTEResult_LockTracking
{
    static void Prefix(HitData __instance)
    {
        if (__instance.combatMove == null) return;
        string name = __instance.combatMove.name;
        if (name.Contains("Moonrang") || name.Contains("Soonrang") || name.Contains("FanOfKnives"))
            LockTracker.OnHitApplied(__instance, name);
    }
}
