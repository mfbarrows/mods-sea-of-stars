using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PerfectTimingJugglenaut.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// LockTracker  –  "set of enemies pending a Jugglecore deflect" approach
//
// Lifecycle
//   Init  (move start, DoMove Prefix):
//     Set is cleared; enemies are added lazily via OnProjectileReachedEnemy.
//
//   Hit landed (HitData.SetQTEResult, filtered to Jugglenaut/Jugglecore):
//     Remove the hit enemy from the set.
//     OnLocksChanged fires immediately after if a lock broke.
//
//   OnLocksChanged (lock state changed on an enemy):
//     If the enemy still has Jugglecore-matching locks → re-add to the set.
//     If no matching locks remain → leave them removed.
//
//   Grant auto-time:
//     While the set is non-empty.
//     Force stop (result = false) when empty.
// ─────────────────────────────────────────────────────────────────────────────

static class LockTracker
{
    internal static readonly HashSet<IntPtr> EnemiesPendingHit = new();

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

    // ── Per-hit update (called from HitData.SetQTEResult) ────────────────────

    /// <summary>
    /// Remove the hit enemy from EnemiesPendingHit.
    /// OnLocksChanged will re-add them if they still have matching locks.
    /// </summary>
    internal static void OnHitApplied(HitData hitData)
    {
        try
        {
            var enemy = hitData.target?.owner?.TryCast<EnemyCombatActor>();
            if (enemy == null) return;
            IntPtr ptr = enemy.Pointer;
            EnemiesPendingHit.Remove(ptr);
            Plugin.LogI(
                $"[LockTracker] Jugglecore deflect hit 0x{ptr:X} → removed " +
                $"(pending={EnemiesPendingHit.Count})");
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[LockTracker] OnHitApplied ERROR: {ex.Message}");
        }
    }

    // ── Per-lock-change update (called from OnLocksChanged) ──────────────────

    /// <summary>
    /// Re-adds the enemy if they still have matching Jugglecore locks — meaning
    /// they need another deflect hit. If no locks remain they stay removed.
    /// </summary>
    internal static void UpdateForEnemy(EnemyCombatActor enemy)
    {
        try
        {
            IntPtr ptr = enemy.Pointer;
            bool hasLocks = EnemyHasMatchingLocks(
                enemy, JugglecoreCycleFlag.MoveDamageTypes);

            if (hasLocks) EnemiesPendingHit.Add(ptr);

            Plugin.LogI(
                $"[LockTracker] OnLocksChanged 0x{ptr:X}: " +
                $"juggleCoreLocks={hasLocks} | pending={EnemiesPendingHit.Count}");
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
/// Re-adds the enemy to EnemiesPendingHit if they still have matching Jugglecore locks.
/// </summary>
[HarmonyPatch(typeof(EnemyCombatActor), nameof(EnemyCombatActor.OnLocksChanged))]
static class Patch_EnemyCombatActor_OnLocksChanged
{
    static void Postfix(EnemyCombatActor __instance)
        => LockTracker.UpdateForEnemy(__instance);
}

/// <summary>
/// Fires when a hit is applied to a target with a QTE result attached.
/// Removes the hit enemy from EnemiesPendingHit (Jugglenaut/Jugglecore moves only).
/// OnLocksChanged will re-add them if they still have locks remaining.
/// </summary>
[HarmonyPatch(typeof(HitData), nameof(HitData.SetQTEResult))]
static class Patch_HitData_SetQTEResult_LockTracking
{
    static void Prefix(HitData __instance)
    {
        if (__instance.combatMove == null) return;
        string name = __instance.combatMove.name;
        if (name.Contains("Jugglenaut") || name.Contains("Jugglecore"))
            LockTracker.OnHitApplied(__instance);
    }
}
