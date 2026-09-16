using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;

namespace CherryPick;

public partial class CherryPick : ResoniteMod
{
    public override string Name => "<color=hero.green>🍃</color><color=hero.red>🍒</color> CherryPick"; // May remove this flair if it gets obnoxious
    public override string Author => "Cyro";
    public override string Version => typeof(CherryPick).Assembly!.GetName().Version!.ToString();
    public override string Link => "https://github.com/BlueCyro/CherryPick";
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
                // This is mostly just cosmetic so that the static constructor runs.
                CherryPicker.SetReady();
            });
        });
    }
}
