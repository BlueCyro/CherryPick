using System.Reflection;
using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;

namespace CherryPick;

public struct WorkerDetails(string name, string? path, Type type)
{
    public readonly string Name => name;
    public readonly string Path => path ?? "";
    public readonly Type Type => type;
}

public class CherryPicker(string? scope = null)
{
    public string? Scope
    {
        get => scope; 
        set
        {
            scope = value;
            WarmScope(value);
        }
    }
    public Dictionary<string, WorkerDetails> Workers => scope != null ? _pathCache[scope] : _allWorkers;
    private static readonly Dictionary<string, Dictionary<string, WorkerDetails>> _pathCache = new();
    private static readonly Dictionary<string, WorkerDetails> _allWorkers = new();

    static CherryPicker()
    {
        foreach (var worker in WorkerInitializer.Workers)
        {
            string? path = worker.GetCustomAttribute<CategoryAttribute>()?.Paths[0];
            var detail = new WorkerDetails(worker.GetNiceName(), path, worker);

            _allWorkers[detail.Name.ToLower()] = detail;
        }
    }

    public static void WarmScope(string? scope = null)
    {
        if (!string.IsNullOrEmpty(scope) && !_pathCache.ContainsKey(scope!))
            _pathCache.Add(scope!, _allWorkers.Where(w => w.Value.Path.StartsWith(scope)).ToDictionary(p => p.Key, p => p.Value));
    }

    public IEnumerable<WorkerDetails> PerformMatch(string query)
    {
        string lowerQuery = query.ToLower();
        
        return Workers
            .OrderByDescending(w => MatchRatio(w.Key, lowerQuery))
            .Take(10)
            .Where(w => MatchRatio(w.Key, lowerQuery) > 0f)
            .Select(w => w.Value);
    }

    static float MatchRatio(string result, string match)
    {
        return result.Contains(match) ? (float)match.Length / result.Length : 0f;
    }

    public void EditStart(Slot searchRoot, Slot defaultRoot, CherryPicker picker, Sync<string> scope)
    {
        picker.Scope = scope;
        if (defaultRoot != null && searchRoot != null)
        {
            defaultRoot.ActiveSelf = false;
            searchRoot.ActiveSelf = true;
        }
    }

    public void EditFinished(TextEditor editor, Slot searchRoot, Slot defaultRoot, bool forceFinish = false)
    {
        if (forceFinish)
            editor.Text.Target.Text = null;
         
        if (editor != null &&
            editor.Text.Target != null &&
            string.IsNullOrEmpty(editor.Text.Target.Text) &&
            defaultRoot != null &&
            searchRoot != null)
        {
            defaultRoot.ActiveSelf = true;
            searchRoot.ActiveSelf = false;
        }
    }

    public void EditChanged(TextEditor editor, Slot searchRoot, Slot defaultRoot, ButtonEventHandler<string> onGenericPressed, ButtonEventHandler<string> onAddPressed)
    {
        if (searchRoot != null &&
            defaultRoot != null && 
            editor != null &&
            onGenericPressed != null &&
            onAddPressed != null)
        {
            string txt = editor.Text.Target.Text;
            if (txt == null)
                return;
            
            UIBuilder searchBuilder = new(searchRoot);
            RadiantUI_Constants.SetupEditorStyle(searchBuilder);
            searchBuilder.Style.TextAlignment = Alignment.MiddleLeft;
            searchBuilder.Style.ButtonTextAlignment = Alignment.MiddleLeft;
            searchBuilder.Style.MinHeight = 48f;

            var results = PerformMatch(editor.Text.Target.Text);

            searchRoot.DestroyChildren();

            searchBuilder.Style.TextLineHeight = 1f;
            foreach (var result in results)
            {
                string arg = result.Type.IsGenericTypeDefinition ? Path.Combine(result.Path, result.Type.FullName) : result.Type.FullName;
                var pressed = result.Type.IsGenericTypeDefinition ? onGenericPressed : onAddPressed;

                var button = searchBuilder.Button(result.Name, RadiantUI_Constants.Sub.CYAN, pressed, arg, 0f);

                if (result.Type.IsGenericTypeDefinition)
                    button.LocalPressed += (b, d) => EditFinished(editor, searchRoot, defaultRoot, true);
                
                var text = (Text)button.LabelTextField.Parent;
                text.Size.Value = 24.44582f;

                var smooth = button.Slot.AttachComponent<SmoothValue<colorX>>();
                IField<colorX> target = button.ColorDrivers.First().ColorDrive.Target;
                smooth.TargetValue.Value = target.Value;

                button.ColorDrivers.First().ColorDrive.Target = smooth.TargetValue;
                smooth.Value.Target = target;
                smooth.Speed.Value = 12f;
            }
        }
    }
}