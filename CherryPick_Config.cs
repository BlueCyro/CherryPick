using ResoniteModLoader;

namespace CherryPick;

public partial class CherryPick : ResoniteMod
{
    public const int MAX_RESULT_COUNT = 120;

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> Enabled = new("Enabled", "When checked, enables CherryPick", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> SingleClick_Config = new("Single-click search result buttons", "When checked, search results will only require a single click to select. Otherwise, double click", () => true);
    public static float PressDelay => Config!.GetValue(SingleClick_Config) ? 0f : 0.35f;
    public static bool SingleClick => Config!.GetValue(SingleClick_Config);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> SelectorFlair = new("Selector flair", "When checked, enables a small flair on the slot name of the selector. Disable if this causes issues", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> ResultCount = new("Result count", $"How many results to show when searching (clamped to {MAX_RESULT_COUNT})", () => 10);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> ClearFocus = new("Clear focus", "When checked, the search buttons will clear the focus of the search bar.", () => true);
}