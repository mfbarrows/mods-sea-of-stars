namespace SeraiDefaultSkin.Patches;

/// <summary>
/// Flip Enabled to true to activate verbose diagnostic logging.
/// Being a const, the compiler eliminates all guarded bodies when false —
/// zero overhead in the shipped build.
/// </summary>
static class Diag
{
    internal const bool Enabled = true;
}
