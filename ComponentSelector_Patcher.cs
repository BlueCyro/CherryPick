using HarmonyLib;
using System.Reflection;
using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;

namespace CherryPick;


[HarmonyPatch(typeof(ComponentSelector), "SetupUI")]
public static class ComponentSelector_Patcher
{
    [HarmonyPrefix]
    public static bool SetupUI_Prefix(ComponentSelector __instance, LocaleString title, float2 size, SyncRef<Slot> ____uiRoot, Sync<string> ____rootPath)
    {
        if (!CherryPick.Config!.GetValue(CherryPick.Enabled))
            return true;


        var onAddPressed =
            __instance.GetType()
            .GetMethod("OnAddComponentPressed", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.CreateDelegate(typeof(ButtonEventHandler<string>), __instance) as ButtonEventHandler<string>;


        var onGenericPressed =
            __instance.GetType()
            .GetMethod("OpenGenericTypesPressed", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.CreateDelegate(typeof(ButtonEventHandler<string>), __instance) as ButtonEventHandler<string>;


        if (onAddPressed == null || onGenericPressed == null)
            return true;


        // Yeah.
        if (CherryPick.Config!.GetValue(CherryPick.SelectorFlair))
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
        ButtonValueSet<string> nullSet = nullButton.Slot.AttachComponent<ButtonValueSet<string>>();

        TextEditor editor = field.Editor.Target;
        Sync<string> fieldText = (editor.Text.Target as Text)!.Content;
        nullSet.TargetValue.Target = fieldText;

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
        field.Text.ParseRichText.Value = false;
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

        __instance.BuildUI(null, false, null, false); // Build the normal component selector UI


        UIBuilder searchBuilder = new(searchRoot);
        CherryPicker picker = new(searchRoot, ____uiRoot, onGenericPressed, onAddPressed, searchBuilder, ____rootPath);


        RadiantUI_Constants.SetupEditorStyle(searchBuilder);
        searchBuilder.Style.TextAlignment = Alignment.MiddleLeft;
        searchBuilder.Style.ButtonTextAlignment = Alignment.MiddleLeft;
        searchBuilder.Style.MinHeight = 80f;
        searchBuilder.Style.TextLineHeight = 1f;


        // field.Editor.Target.LocalEditingStarted += picker.EditStart;
        // field.Editor.Target.LocalEditingChanged += picker.EditChanged;
        // field.Editor.Target.LocalEditingFinished += picker.EditFinished;


        fieldText.Changed += c => picker.EditChanged(editor);


        // void nullButtonPressed(IButton b, ButtonEventData d)
        // {
        //     field.Text.Content.Value = null;
        //     field.Editor.Target.ForceEditingChangedEvent();
        // }


        // void nullButtonDestroyed(IDestroyable d)
        // {
        //     IButton destroyedButton = (IButton)d;

        //     destroyedButton.LocalPressed -= nullButtonPressed;
        //     destroyedButton.Destroyed -= nullButtonDestroyed;
        // }


        // nullButton.LocalPressed += nullButtonPressed;
        // nullButton.Destroyed += nullButtonDestroyed;


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


[HarmonyPatch(typeof(World), "Types", MethodType.Getter)]
public static class World_Patcher
{
    [HarmonyReversePatch]
    public static TypeManager Types(this World w) => throw new NotImplementedException("Harmony stub");
}