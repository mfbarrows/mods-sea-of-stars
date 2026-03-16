using System;
using HarmonyLib;

namespace PerfectTimingJugglenaut.Patches;

// ─────────────────────────────────────────────────────────────────────────────
// Jugglecore / Jugglenaut auto-time patches
//
// Strategy: mirrors FanOfKnives exactly.
//
// OnProjectileReachedEnemy fires each time a JugglenautProjectile hits its
// target (CombatTarget).  We read projectile.MainTarget to identify the enemy
// and add them to EnemiesPendingHit when they have matching spell locks.
// This seeds the pending set reliably — MainTarget is stable at point of impact.
//
// DoMove resets DeflectCount to 0 at move start.
//
// CanAutoTimeHit grants auto-time and increments DeflectCount while
// EnemiesPendingHit is non-empty.  Once all lock-relevant enemies have been
// deflected the set empties and we stop, so subsequent projectiles score a
// natural miss and the move ends via its own wind-down logic.
//
// HitData.SetQTEResult (filtered to "Jugglenaut"/"Jugglecore") removes the
// hit enemy from the pending set after a deflect scores.
// OnLocksChanged re-adds an enemy if they still have matching locks.
//
// "Jugglenaut" is the in-game asset name; "Jugglecore" is the class name.
// Both strings are checked when filtering moveDefinition.name.
//
// Field offsets (from dump.cs Jugglecore):
//   none needed — uses projectile.MainTarget public property.
// ─────────────────────────────────────────────────────────────────────────────

static class JugglecoreCycleFlag
{
    internal static int DeflectCount = 0;
    internal static int TargetCount  = 0;

    // Damage types this move deals (populated once per use at DoMove).
    // Read by LockTracker.UpdateForEnemy to decide which enemies to re-add.
    internal static Il2CppSystem.Collections.Generic.List<EDamageType> MoveDamageTypes = null!;
}

/// <summary>
/// Reset DeflectCount at move start so a fresh set of auto-times is granted.
/// </summary>
[HarmonyPatch(typeof(Jugglecore), "DoMove")]
static class Patch_Jugglecore_DoMove
{
    static void Prefix(Jugglecore __instance)
    {
        JugglecoreCycleFlag.DeflectCount = 0;
        JugglecoreCycleFlag.TargetCount  = 0;

        JugglecoreCycleFlag.MoveDamageTypes =
            new Il2CppSystem.Collections.Generic.List<EDamageType>();
        __instance.GetLocksDamageTypes(JugglecoreCycleFlag.MoveDamageTypes);

        // Clear pending set from any previous move.
        LockTracker.EnemiesPendingHit.Clear();

        Plugin.LogI(
            $"[Jugglecore.DoMove] PRE  | DeflectCount reset, " +
            $"MoveDamageTypes.Count={JugglecoreCycleFlag.MoveDamageTypes.Count}");
    }
}

/// <summary>
/// After each JugglenautProjectile reaches its target (the outbound hit), read
/// projectile.MainTarget to identify the enemy and add them to EnemiesPendingHit
/// if they have matching spell locks.
///
/// This fires before the bounce-back deflect window opens, so the pending set
/// is populated in time for CanAutoTimeHit to grant the deflect.
///
/// The same projectile may reach an enemy multiple times (re-throws).  HashSet
/// deduplication plus the lock-presence check ensure we only add enemies that
/// still need a lock-breaking deflect.
/// </summary>
[HarmonyPatch(typeof(Jugglecore), "OnProjectileReachedEnemy")]
static class Patch_Jugglecore_OnProjectileReachedEnemy
{
    static void Postfix(Projectile projectile)
    {
        try
        {
            var target = projectile?.MainTarget;
            if (target == null) return;
            var enemy = target.owner?.TryCast<EnemyCombatActor>();
            if (enemy == null) return;

            bool hasLocks = LockTracker.EnemyHasMatchingLocks(
                enemy, JugglecoreCycleFlag.MoveDamageTypes);

            if (hasLocks)
            {
                LockTracker.EnemiesPendingHit.Add(enemy.Pointer);

                // Track unique targets for count-based fallback.
                JugglecoreCycleFlag.TargetCount =
                    Math.Max(JugglecoreCycleFlag.TargetCount,
                             LockTracker.EnemiesPendingHit.Count);

                Plugin.LogI(
                    $"[Jugglecore.OnProjectileReachedEnemy] POST | " +
                    $"enemy 0x{enemy.Pointer:X} has locks -> pending={LockTracker.EnemiesPendingHit.Count}");
            }
            else
            {
                Plugin.LogI(
                    $"[Jugglecore.OnProjectileReachedEnemy] POST | " +
                    $"enemy 0x{enemy.Pointer:X} no matching locks -> not added");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogI($"[Jugglecore.OnProjectileReachedEnemy] POST | ERROR: {ex.Message}");
        }
    }
}

/// <summary>
/// For Jugglecore / Jugglenaut moves: grant auto-time while EnemiesPendingHit
/// is non-empty (at least one enemy still needs a lock-breaking deflect), then
/// stop so the move ends naturally on a miss.
/// Mirrors Patch_CanAutoTimeHit_FanOfKnives in PerfectTimingVenomFlurry.
/// </summary>
[HarmonyPatch(typeof(AbstractTimedAttackHandler), "CanAutoTimeHit")]
static class Patch_CanAutoTimeHit_Jugglecore
{
    static void Postfix(PlayerCombatMoveDefinition moveDefinition, ref bool __result)
    {
        if (moveDefinition == null) return;
        string name = moveDefinition.name;
        if (!name.Contains("Jugglenaut") && !name.Contains("Jugglecore"))
            return; // other moves handled by PerfectTimingAttack

        int deflects = JugglecoreCycleFlag.DeflectCount;

        // Safety cap — should never be reached in normal play.
        if (deflects >= 20)
        {
            __result = false;
            Plugin.LogI(
                $"[CanAutoTimeHit] Jugglecore | SAFETY CAP: DeflectCount={deflects} >= 20 -- forcing stop");
            return;
        }

        int pending = LockTracker.EnemiesPendingHit.Count;

        // No enemies left in the pending set.
        if (pending == 0)
        {
            int needed = JugglecoreCycleFlag.TargetCount - 1;
            if (deflects < needed)
            {
                // Pending set emptied before all targets were hit (e.g. boss limbs sharing
                // an owner pointer). Fall back to count-based: keep granting auto-time.
                __result = true;
                JugglecoreCycleFlag.DeflectCount++;
                Plugin.LogW(
                    $"[CanAutoTimeHit] Jugglecore | WARNING: pending empty but deflects={deflects} " +
                    $"< needed={needed} -- granting fallback auto-time");
                return;
            }
            __result = false;
            Plugin.LogI(
                $"[CanAutoTimeHit] Jugglecore | pending set empty (DeflectCount={deflects}) -> stop");
            return;
        }

        // Pending enemies exist — grant auto-time.
        __result = true;
        JugglecoreCycleFlag.DeflectCount++;
        Plugin.LogI(
            $"[CanAutoTimeHit] Jugglecore | pending={pending} DeflectCount={deflects} -> auto-time " +
            $"(DeflectCount now {JugglecoreCycleFlag.DeflectCount})");
    }
}
