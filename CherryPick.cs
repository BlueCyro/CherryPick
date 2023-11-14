using HarmonyLib;
using ResoniteModLoader;
using System.Reflection;
using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;
using FrooxEngine.ProtoFlux;

namespace CherryPick;

public class CherryPick : ResoniteMod
{
    public override string Name => "<color=hero.green>🍃</color><color=hero.red>🍒</color> CherryPick"; // May remove this flair if it gets obnoxious
    public override string Author => "Cyro";
    public override string Version => "1.0.2";
    public override string Link => "resonite.com";
    public static ModConfiguration? Config;

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> Enabled = new("Enabled", "When checked, enables CherryPick", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> SingleClick = new("Single-click search result buttons", "When checked, search results will only require a single click to select. Otherwise, double click", () => true);
   
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> SelectorFlair = new("Selector flair", "When checked, enables a small flair on the slot name of the selector. Disable if this causes issues", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> ResultCount = new("Result count", "How many results to show when searching (clamped to 40)", () => 10);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> ClearFocus = new("Clear focus", "When checked, the search buttons will clear the focus of the search bar.", () => true);

    public override void OnEngineInit()
    {
        Harmony harmony = new("net.Cyro.CherryPick");
        Config = GetConfiguration();
        Config?.Save(true);
        harmony.PatchAll();

        // Scan for workers only after FrooxEngine is fully initialized
        Engine.Current.RunPostInit(() =>
        {
            CherryPicker.WarmScope(); // Initialize class to warm up those code paths all nice and toasty (so we don't hitch when first spawning a component selector)
            CherryPicker.WarmScope(ProtoFluxHelper.PROTOFLUX_ROOT);
        });
    }

    [HarmonyPatch(typeof(ComponentSelector), "SetupUI")]
    public static class ComponentSelector_Patcher
    {
        [HarmonyPrefix]
        public static bool SetupUI_Prefix(ComponentSelector __instance, LocaleString title, float2 size, SyncRef<Slot> ____uiRoot, Sync<string> ____rootPath)
        {
            if (!Config!.GetValue(Enabled))
                return true;
            
            var onAddPressed = __instance.GetType().GetMethod("OnAddComponentPressed", BindingFlags.NonPublic | BindingFlags.Instance)?.CreateDelegate(typeof(ButtonEventHandler<string>), __instance) as ButtonEventHandler<string>;
            var onGenericPressed = __instance.GetType().GetMethod("OpenGenericTypesPressed", BindingFlags.NonPublic | BindingFlags.Instance)?.CreateDelegate(typeof(ButtonEventHandler<string>), __instance) as ButtonEventHandler<string>;

            if (onAddPressed == null || onGenericPressed == null)
                return true;
            
            // Yeah.
            if (Config!.GetValue(SelectorFlair))
                __instance.Slot.Name = $"<color=hero.green>🍃</color><color=hero.red>🍒</color> {__instance.LocalUser.UserName}'s CherryPicked {__instance.Slot.Name}";
            

            var builder = RadiantUI_Panel.SetupPanel(__instance.Slot, title, size, true, true);
            RadiantUI_Constants.SetupEditorStyle(builder, true);
            builder.Style.TextAlignment = Alignment.MiddleLeft;
            builder.Style.ForceExpandHeight = false;
            builder.Style.TextLineHeight = 1f;


            builder.VerticalLayout(7.28605f, 7.28605f); // UI design magic funny look-good values.
            builder.Style.MinHeight = 64f;
            builder.HorizontalLayout(7.28605f);
            builder.Style.MinWidth = 64f;
            builder.Style.FlexibleWidth = 1f;
            var field = builder.TextField(null, true, "Undo text field search", true, $"<alpha=#77>Search..."); // Make the search field

            builder.Style.FlexibleWidth = 0f;

            Button nullButton = builder.Button("∅");
            SmoothButton(nullButton);

            builder.NestOut();

            // Small tweak to make the caret smoothly blink
            var smooth = field.Slot.AttachComponent<SmoothValue<colorX>>();
            smooth.Value.Target = field.Text.CaretColor;
            smooth.WriteBack.Value = true;
            smooth.Speed.Value = 12f;
            
            Button button = field.Slot.GetComponent<Button>();
            SmoothButton(button);

            // Finish up the field text sizing, also prepare for the actual component build area
            field.Text.HorizontalAutoSize.Value = true;
            field.Text.Size.Value = 39.55418f;
            field.Text.Color.Value = RadiantUI_Constants.Neutrals.LIGHT;
            field.Editor.Target.FinishHandling.Value = TextEditor.FinishAction.NullOnWhitespace;
            builder.Style.FlexibleHeight = 1f;

            // Make an overlapping layout so we can easily disable and enable the area where components are searched
            builder.OverlappingLayout(0f);
            builder.ScrollArea();
            Slot searchRoot = builder.Root;
            builder.VerticalLayout(7.28605f, 0f);
            builder.FitContent(SizeFit.Disabled, SizeFit.MinSize);
            
            searchRoot.ActiveSelf = false;

            // Back up a bit and make the area for the normal component browser UI to generate
            builder.NestOut();
            builder.ScrollArea();
            builder.VerticalLayout(7.28605f, 7.28605f);
            builder.FitContent(SizeFit.Disabled, SizeFit.MinSize);
            ____uiRoot.Target = builder.Root;

            __instance.BuildUI(null, false, null, false);

            CherryPicker picker = new();

            UIBuilder searchBuilder = new(searchRoot);
            RadiantUI_Constants.SetupEditorStyle(searchBuilder);
            searchBuilder.Style.TextAlignment = Alignment.MiddleLeft;
            searchBuilder.Style.ButtonTextAlignment = Alignment.MiddleLeft;
            searchBuilder.Style.MinHeight = 80f;
            searchBuilder.Style.TextLineHeight = 1f;

            field.Editor.Target.LocalEditingStarted += c => picker.EditStart(searchRoot, ____uiRoot, picker, ____rootPath);

            field.Editor.Target.LocalEditingChanged += c => picker.EditChanged(c, searchRoot, ____uiRoot, searchBuilder, onGenericPressed, onAddPressed);

            field.Editor.Target.LocalEditingFinished += c => picker.EditFinished(c, searchRoot, ____uiRoot);

            nullButton.LocalPressed += (b, d) =>
            {
                field.Text.Content.Value = null;
                field.Editor.Target.ForceEditingChangedEvent();
            };

            return false;
        }

        static void SmoothButton(Button b, float speed = 12f)
        {
            FieldDrive<colorX>? driver = b.ColorDrivers.FirstOrDefault()?.ColorDrive;
            IField<colorX>? targetField = driver?.Target;

            // Make the button smoooooooth
            if (driver != null && driver.Target != null)
            {
                var smooth = b.Slot.AttachComponent<SmoothValue<colorX>>();
                smooth.TargetValue.Value = driver.Target.Value;
                driver.Target = smooth.TargetValue;
                smooth.Value.Target = targetField;
                smooth.Speed.Value = speed;             
            }
        }
    }
}
