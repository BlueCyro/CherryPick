using HarmonyLib;
using ResoniteModLoader;
using System.Reflection;
using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;
using MonoMod.Utils;
using FrooxEngine.ProtoFlux;

namespace CherryPick;

public class CherryPick : ResoniteMod
{
    public override string Name => "CherryPick";
    public override string Author => "Cyro";
    public override string Version => "1.0.0";
    public override string Link => "resonite.com";
    public static ModConfiguration? Config;

    public override void OnEngineInit()
    {
        Harmony harmony = new("net.Cyro.CherryPick");
        Config = GetConfiguration();
        Config?.Save(true);
        harmony.PatchAll();
        
        CherryPicker.WarmScope(); // Initialize class to warm up those code paths all nice and toasty (so we don't hitch when first spawning a component selector)
        CherryPicker.WarmScope(ProtoFluxHelper.PROTOFLUX_ROOT);
    }

    [HarmonyPatch(typeof(ComponentSelector))]
    public static class ComponentSelector_Patcher
    {
        [HarmonyPrefix]
        [HarmonyPatch("SetupUI")]
        public static bool SetupUI_Prefix(ComponentSelector __instance, LocaleString title, float2 size, SyncRef<Slot> ____uiRoot, Sync<string> ____rootPath)
        {
            var onAddPressed = __instance.GetType().GetMethod("OnAddComponentPressed", BindingFlags.NonPublic | BindingFlags.Instance)?.CreateDelegate<ButtonEventHandler<string>>(__instance);
            var onGenericPressed = __instance.GetType().GetMethod("OpenGenericTypesPressed", BindingFlags.NonPublic | BindingFlags.Instance)?.CreateDelegate<ButtonEventHandler<string>>(__instance);

            if (onAddPressed == null || onGenericPressed == null)
                return true;
            
            var builder = RadiantUI_Panel.SetupPanel(__instance.Slot, title, size, true, true);
            RadiantUI_Constants.SetupEditorStyle(builder, true);
            builder.Style.TextAlignment = Alignment.MiddleLeft;
            builder.Style.ForceExpandHeight = false;
            builder.Style.TextLineHeight = 1f;


            builder.VerticalLayout(7.28605f, 7.28605f);
            builder.Style.MinHeight = 64f;
            var field = builder.TextField(null, true, "Undo text field search", false, $"<alpha=#77>Search...");
            

            Button button = field.Slot.GetComponent<Button>();
            FieldDrive<colorX>? driver = button.ColorDrivers.FirstOrDefault()?.ColorDrive;
            IField<colorX>? targetField = driver?.Target;

            if (driver != null && driver.Target != null)
            {
                var smooth = field.Slot.AttachComponent<SmoothValue<colorX>>();
                smooth.TargetValue.Value = driver.Target.Value;
                driver.Target = smooth.TargetValue;
                smooth.Value.Target = targetField;
                smooth.Speed.Value = 12f;
            }


            field.Text.AutoSize = false;
            field.Text.Size.Value = 39.55418f;
            field.Text.Color.Value = RadiantUI_Constants.Neutrals.LIGHT;
            field.Editor.Target.FinishHandling.Value = TextEditor.FinishAction.NullOnWhitespace;
            builder.Style.FlexibleHeight = 1f;


            builder.OverlappingLayout(0f);
            builder.ScrollArea();
            Slot searchRoot = builder.Root;
            builder.VerticalLayout(7.28605f, 0f);
            builder.FitContent(SizeFit.Disabled, SizeFit.MinSize);


            for (int i = 0; i < 10; i++)
            {
                builder.Text("TEST TEST");
            }
            searchRoot.ActiveSelf = false;


            builder.NestOut();
            builder.ScrollArea();
            builder.VerticalLayout(7.28605f, 7.28605f);
            builder.FitContent(SizeFit.Disabled, SizeFit.MinSize);
            ____uiRoot.Target = builder.Root;

            __instance.BuildUI(null, false, null, false);

            CherryPicker picker = new();

            field.Editor.Target.LocalEditingStarted += c => picker.EditStart(searchRoot, ____uiRoot, picker, ____rootPath);

            field.Editor.Target.LocalEditingChanged += c => picker.EditChanged(field.Editor, searchRoot, ____uiRoot, onGenericPressed, onAddPressed);

            field.Editor.Target.LocalEditingFinished += c => picker.EditFinished(c, searchRoot, ____uiRoot);
            

            return false;
        }
    }
}
