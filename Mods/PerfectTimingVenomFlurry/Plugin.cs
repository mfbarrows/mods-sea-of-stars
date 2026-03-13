using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System;

namespace PerfectTimingVenomFlurry;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    private Harmony _harmony = null!;

    internal static void LogD(string msg) => Log.LogDebug($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
    internal static void LogI(string msg) => Log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
    internal static void LogW(string msg) => Log.LogWarning($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
    internal static void LogE(string msg) => Log.LogError($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");

    public override void Load()
    {
        Log = base.Log;

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);

        LogI($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded.");
    }

    public override bool Unload()
    {
        _harmony.UnpatchSelf();
        return base.Unload();
    }
}
