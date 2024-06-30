using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

namespace CherryPick;

public partial class CherryPick : ResoniteMod
{
    public override string Name => "<color=hero.green>🍃</color><color=hero.red>🍒</color> CherryPick"; // May remove this flair if it gets obnoxious
    public override string Author => "Cyro";
    public override string Version => typeof(CherryPick).Assembly.GetName().Version.ToString();
    public override string Link => "https://github.com/RileyGuy/CherryPick";
    public static ModConfiguration? Config;

    public override void OnEngineInit()
    {
        Harmony harmony = new("net.Cyro.CherryPick");
        Config = GetConfiguration();
        Config?.Save(true);
        harmony.PatchAll();

        // Scan for workers only after FrooxEngine is fully initialized
        Engine.Current.RunPostInit(() =>
        {
            Task.Run(() =>
            {
                // CherryPicker.WarmScope(); // Initialize class to warm up those code paths all nice and toasty (so we don't hitch when first spawning a component selector)
                // CherryPicker.WarmScope(ProtoFluxHelper.PROTOFLUX_ROOT);
                CherryPicker.SetReady();
            });
        });
    }
}