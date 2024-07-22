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

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<string> UserExcludedCategories = new("Excluded categories", "Excludes specific categories from being searched into by path. Separate entries by semicolon. Search will work when started inside them", () => "/ProtoFlux, /Example/Test");

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> SearchRefreshDelay = new("Search refresh delay", "Time to wait after search input change before refreshing the results. 0 to always refresh.", () => .4f);

}