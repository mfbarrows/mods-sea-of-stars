using HarmonyLib;

namespace FishingMod.Patches;

/// <summary>
/// Skips <see cref="FishermanReelFishState.UpdateReelOutsideZone"/> entirely so that
/// being outside the reel sweet-spot zone no longer:
///   - drains lineHPLeft (no fish loss from line HP)
///   - compresses lineMaxHP (no regen ceiling reduction)
///   - triggers controller rumble
///   - builds camera shake
///   - releases targetZ (no progress rollback on the fish)
/// </summary>
[HarmonyPatch(typeof(FishermanReelFishState), "UpdateReelOutsideZone")]
static class Patch_FishermanReelFishState_UpdateReelOutsideZone
{
    static bool Prefix() => false;
}
