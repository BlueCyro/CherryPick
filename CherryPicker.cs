using System.Reflection;
using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;

namespace CherryPick;

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

    public static bool IsReady { get; private set; }
    public List<WorkerDetails> Workers => scope != null ? _pathCache[scope] : _allWorkers;
    private static readonly Dictionary<string, List<WorkerDetails>> _pathCache = new();
    private static readonly  List<WorkerDetails> _allWorkers = new();
    private readonly List<WorkerDetails> _results = new();

    static CherryPicker()
    {
        foreach (var worker in WorkerInitializer.Workers)
        {
            if (!typeof(Component).IsAssignableFrom(worker))
                continue;
            
            string? path = worker.GetCustomAttribute<CategoryAttribute>()?.Paths[0];
            var detail = new WorkerDetails(worker.GetNiceName(), path, worker);

            _allWorkers.Add(detail);
        }
    }



    public static void SetReady()
    {
        IsReady = true;
    }



    // Makes a new pre-filtered list that is scoped to whatever the string is. This is somewhat heavy, so it's done when the mod initializes.
    public static void WarmScope(string? scope = null)
    {
        if (!string.IsNullOrEmpty(scope) && !_pathCache.ContainsKey(scope!))
        {
            var filteredDict = _allWorkers
                                .Where(w => w.Path.StartsWith(scope))
                                .ToList();

            _pathCache.Add(scope!, filteredDict);
        }
    }



    public void PerformMatch(string query, int resultCount = 10)
    {
        _results.Clear();

        // Occam's razor on this fellas. Sometimes the simplest solution is the right one. This takes like 3-7ms for the first sweep, then 0ms on subsequent queries. Wacky.
        var results = Workers
            .Select(w => new { worker = w, ratio = MatchRatioInsensitive(w.LowerName, query) })
            .Where(x => x.ratio > 0f)
            .OrderByDescending(x => x.ratio)
            .Take(resultCount)
            .Select(x => x.worker);
        
        foreach (var w in results)
        {
            _results.Add(w);
        }
    }



    // Out of the total string length, how many characters actually match the query. Gives decent results.
    static float MatchRatioInsensitive(string? result, string match) 
    {
        if (result == null)
            return 0f;
        
        var contains = match.Split(' ').Select(item => (item, score: result.IndexOf(item, StringComparison.OrdinalIgnoreCase)));
        
        var score = contains
                    .Where(v => v.score >= 0)
                    .Select((v, i) => (v.item, v.score, i))
                    .Sum((v) => (float)v.item.Length / (result.Length + v.i + 1));

        return contains.All(v => v.score >= 0) ? score : 0f;
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



    public void EditChanged(TextEditor editor, Slot searchRoot, Slot defaultRoot, UIBuilder searchBuilder, ButtonEventHandler<string> onGenericPressed, ButtonEventHandler<string> onAddPressed)
    {
        if (searchRoot == null ||
            defaultRoot == null || 
            editor == null ||
            onGenericPressed == null ||
            onAddPressed == null || 
            searchBuilder == null ||
            !IsReady // You can't search until the cache is built! This is fine in most cases, but if you end up searching before then, too bad!
        )
                return;
            
        string txt = editor.Text.Target.Text;
        if (txt == null)
            return;


        int tickIndex = txt.IndexOf('`');
        string matchTxt = tickIndex > 0 ? txt.Substring(0, tickIndex) : txt;

        searchRoot.DestroyChildren();
        int resultCount = CherryPick.Config!.GetValue(CherryPick.ResultCount);
        resultCount = resultCount <= 40 ? resultCount : 40;

        PerformMatch(matchTxt, resultCount);

        foreach (var result in _results)
        {
            string arg = result.Type.IsGenericTypeDefinition ? Path.Combine(result.Path, result.Type.FullName) : result.Type.FullName;
            var pressed = result.Type.IsGenericTypeDefinition ? onGenericPressed : onAddPressed;

            CreateButton(result, pressed, arg, searchBuilder, editor, searchRoot, defaultRoot, RadiantUI_Constants.Sub.CYAN);
        }

        WorkerDetails firstGeneric = _results.FirstOrDefault(w => w.Type.IsGenericTypeDefinition);

        if (firstGeneric.Type != null && tickIndex > 0)
        {
            string newTxt = txt.Substring(MathX.Clamp(tickIndex + 2, 0, txt.Length));
            string typeName = firstGeneric.Type.FullName + newTxt;
            Type? constructed = null;

            try
            {
                constructed = WorkerManager.GetType(typeName);
            }
            catch (Exception) { }; // Lazy way to get around accidentally making types with too few parameters... Probably bad. I think. Possibly.

            if (constructed != null)
            {
                WorkerDetails detail = new(constructed.GetNiceName(), firstGeneric.Path, constructed);
                Button typeButton = CreateButton(detail, onAddPressed, typeName, searchBuilder, editor, searchRoot, defaultRoot, RadiantUI_Constants.Sub.ORANGE);
                typeButton.Slot.OrderOffset = -1024;
            }
        }
    }



    // One day we will have better UI construction... one day :')
    private Button CreateButton(WorkerDetails detail, ButtonEventHandler<string> pressed, string arg, UIBuilder builder, TextEditor editor, Slot searchRoot, Slot defaultRoot, colorX col)
    {
        string path = Scope != null ? detail.Path.Replace(Scope, null) : detail.Path;

        var button = builder.Button($"<noparse={detail.Name.Length}>{detail.Name}<br><size=61.803%><line-height=133%>{path}", col, pressed, arg, CherryPick.PressDelay);
        ValueField<double> lastPressed = button.Slot.AddSlot("LastPressed").AttachComponent<ValueField<double>>();
        
        button.ClearFocusOnPress.Value = CherryPick.Config!.GetValue(CherryPick.ClearFocus);

        if (detail.Type.IsGenericTypeDefinition)
        {
            button.LocalPressed += (b, d) =>
            {
                double now = searchRoot.World.Time.WorldTime;
                if (now - lastPressed.Value < CherryPick.PressDelay || CherryPick.SingleClick)
                    EditFinished(editor, searchRoot, defaultRoot, true);
                else
                    lastPressed.Value.Value = now;
            };
        }
        
        var text = (Text)button.LabelTextField.Parent;
        text.Size.Value = 24.44582f; 

        var smooth = button.Slot.AttachComponent<SmoothValue<colorX>>();
        IField<colorX> target = button.ColorDrivers.First().ColorDrive.Target;
        smooth.TargetValue.Value = target.Value;

        button.ColorDrivers.First().ColorDrive.Target = smooth.TargetValue;
        smooth.Value.Target = target;
        smooth.Speed.Value = 12f;

        return button;
    }
}