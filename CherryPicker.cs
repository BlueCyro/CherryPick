using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;
using static CherryPick.CherryPick;

namespace CherryPick;

public class CherryPicker
{
    public string Scope
    {
        get => _owner._rootPath;
        set => _owner._rootPath.Value = value;
    }

    public static bool IsReady { get; private set; }
    public static WorkerDetails[] Workers => _allWorkers;


    private const string PROTOFLUX_PREFIX = "/ProtoFlux/Runtimes/";
    private static readonly WorkerDetails[] _allWorkers = [];


    static CherryPicker()
    {
        static IEnumerable<CategoryNode<Type>> flatten(IEnumerable<CategoryNode<Type>> categories) =>
            categories
            .SelectMany(category => flatten(category.Subcategories))
            .Concat(categories);


        IEnumerable<CategoryNode<Type>> allCategories = flatten(WorkerInitializer.ComponentLibrary.Subcategories);
        List<WorkerDetails> details = [];

        // using FileStream compFile = File.OpenWrite("./COMPONENTS.txt");
        // using StreamWriter writer = new(compFile);


        foreach (CategoryNode<Type> category in allCategories)
            foreach (Type element in category.Elements)
                if (element.IsDataModelType() && element.AssemblyQualifiedName is { } qualifiedName)
                    details.Add(new WorkerDetails(element, category.GetPath()));

        _allWorkers = [.. details];
    }

    public static void SetReady() => IsReady = true;




    readonly SortedList<float, WorkerDetails> _results; // Not queryable by index due to the implementation of MatchRatioComparer
    readonly UIBuilder _searchResultBuilder;
    readonly ComponentSelector _owner;
    readonly TextEditor _searchEditor;
    readonly Slot _searchRoot;


    public CherryPicker(ComponentSelector owner, TextEditor searchEditor, Slot searchRoot, UIBuilder searchBuilder)
    {
        _owner = owner;
        _searchRoot = searchRoot;
        _searchEditor = searchEditor;
        _searchResultBuilder = searchBuilder;
        _results = new(new MatchRatioComparer());

        _searchEditor.Text.Target.Changed += EditChanged;
        _owner.Destroyed += OnOwnerDestroyed;
    }




    #region String matching



    public void PerformMatch(string query, int resultCount = 10, bool showProtofluxComponents = true)
    {
        ResetResults(resultCount);

        int workerCount = Workers.Length;
        WorkerDetails[] details = Workers;

        string[] splitQuery = query.Split(' ');



        // The for loops are a bit hot and can cause minor
        // hitches if care isn't taken. Avoiding branch logic if possible

        // Check if there's actually anything to filter, because if there isn't, a slightly more efficient loop can be used
        if (string.IsNullOrEmpty(Scope) && showProtofluxComponents)
        {
            for (int i = 0; i < workerCount; i++)
            {
                WorkerDetails worker = details[i];
                float ratio = CherryPick_Helper.MatchRatioInsensitive(worker.LowerName, splitQuery);

                _results.Add(ratio, worker);
                int detailCount = _results.Count;

                _results.RemoveAt(detailCount - 1);
            }
        }
        else
        {
            string searchScope = "/" + Scope;
            for (int i = 0; i < workerCount; i++)
            {
                WorkerDetails worker = details[i];
                float ratio = worker.Path.StartsWith(searchScope) && (showProtofluxComponents || !worker.Path.StartsWith(PROTOFLUX_PREFIX))
                    ? CherryPick_Helper.MatchRatioInsensitive(worker.LowerName, splitQuery)
                    : 0f;

                _results.Add(ratio, worker);
                int detailCount = _results.Count;

                _results.RemoveAt(detailCount - 1);
            }
        }


        // Remove the zero-scored results after the fact. Avoids another conditional in the hot path above
        while (MathX.Approximately(_results.LastOrDefault().Key, 0f) && _results.Count > 0)
            _results.RemoveAt(_results.Count - 1);

    }



    void ResetResults(int startCount = 10)
    {
        _results.Clear();
        for (int i = 0; i < startCount; i++)
            _results.Add(0f, default);
    }

    #endregion



    public void SetPicking(bool picking)
    {
        if (_owner is null || _owner._uiRoot.Target is null || _searchRoot is null)
            return;

        _owner._uiRoot.Target.ActiveSelf = !picking;
        _searchRoot.ActiveSelf = picking;
    }


    public static void ClearSearch(TextEditor editor) => editor.Text.Target.Text = null!;

    public void EditChanged(IChangeable _)
    {
        if (_owner is null
         || _owner._uiRoot is null
         || _searchRoot is null
         || _searchEditor is null
         || _searchResultBuilder is null)
            return;


        string? txt = _searchEditor.Text.Target.Text;
        if (string.IsNullOrEmpty(txt))
        {
            SetPicking(false);
            return;
        }

        SetPicking(true);

        int genStart = txt.IndexOf('<');
        int genEnd = txt.LastIndexOf('>') + 1;
        string? genericTypeParams = null;
        string? matchTxt;

        if (genStart > 0)
        {
            matchTxt = txt[..genStart];

            if (genEnd > 0)
                genericTypeParams = txt[genStart..genEnd];
        }
        else
            matchTxt = txt;


        matchTxt = matchTxt.ToLower(); // Ensure lowercase such that queries like "SpInNeR" will still find "Spinner" by it's lowercase form: "spinner"

        _searchRoot.DestroyChildren();
        int resultCount = Config!.GetValue(ResultCount);
        resultCount = MathX.Min(resultCount, MAX_RESULT_COUNT);

        // Three different possibilities:
        // 1. ProtoFlux Search (Scope is non-empty): Show ProtoFlux (obviously)
        // 2. Component Search (Scope is empty), ProtoFlux is hidden: Don't show ProtoFlux
        // 3. Component Search (Scope is empty), ProtoFlux should be shown (debug mode?): Show ProtoFlux
        bool showProtofluxComponents = !string.IsNullOrEmpty(Scope) || Config!.GetValue(ShowProtofluxInComponentSearch);

        PerformMatch(matchTxt, resultCount, showProtofluxComponents);

        IList<WorkerDetails> foundResults = _results.Values;
        int foundResultCount = foundResults.Count;

        for (int i = 0; i < foundResultCount; i++)
        {
            WorkerDetails result = foundResults[i];
            Type type = result.Type;


            colorX buttonColor;
            string arg;
            ButtonEventHandler<string> pressedHandler;

            if (   genericTypeParams is not null
                && _owner.World.Types.ParseNiceType(result.Name + genericTypeParams, true) is Type genType
                && genType.GetGenericTypeDefinition() == result.Type
                && genType.IsValidGenericType(true))
            {
                arg = _owner.World.Types().EncodeType(genType);
                result = result with { Type = genType };
                buttonColor = RadiantUI_Constants.Sub.GREEN;
                pressedHandler = _owner.OnAddComponentPressed;
            }
            else if (result.IsEmptyGeneric)
            {
                arg = Path.Combine(result.Path, result.AssemblyQualifiedName);
                buttonColor = RadiantUI_Constants.Dark.GREEN;
                pressedHandler = _owner.OpenGenericTypesPressed;
            }
            else
            {
                arg = _owner.World.Types().EncodeType(result.Type);
                buttonColor = RadiantUI_Constants.Sub.CYAN;
                pressedHandler = _owner.OnAddComponentPressed;
            }

            Slot buttonSlot = CreateButton(result, pressedHandler, arg, _searchResultBuilder, _searchEditor, buttonColor).Slot;
            buttonSlot.OrderOffset = i;
        }
    }



    // One day we will have better UI construction... one day :')
    private Button CreateButton(in WorkerDetails detail, ButtonEventHandler<string> pressed, string arg, UIBuilder builder, TextEditor editor, colorX col)
    {
        // Snip the scope off of the beginning of the path if the browser so that it's relative to the scope
        string path = Scope != null ? detail.Path.Replace("/" + Scope, null) : detail.Path;
        string buttonText = $"<noparse={detail.NiceName.Length}>{detail.NiceName}<br><size=61.803%><line-height=133%>{path}";


        var button = builder.Button(buttonText, col, pressed, arg, PressDelay);
        ValueField<ulong> pressProxy = button.Slot.AddSlot("PressProxy").AttachComponent<ValueField<ulong>>();
        ButtonValueShift<ulong> proxyShifter = button.Slot.AttachComponent<ButtonValueShift<ulong>>();
        button.Slot.Name = detail.NiceName;

        proxyShifter.Delta.Value = 1;
        proxyShifter.TargetValue.Target = pressProxy.Value;

        ValueField<double> lastPressed = button.Slot.AddSlot("LastPressed").AttachComponent<ValueField<double>>();
        button.ClearFocusOnPress.Value = Config!.GetValue(CherryPick.ClearFocus);

        if (detail.Type.IsGenericTypeDefinition)
        {
            // Define delegate here for unsubscription later
            void CherryPickButtonPress(IChangeable c)
            {
                double now = _searchRoot.World.Time.WorldTime;


                if (now - lastPressed.Value < CherryPick.PressDelay || CherryPick.SingleClick)
                {
                    SetPicking(false);
                    ClearSearch(editor);
                }
                else
                    lastPressed.Value.Value = now;
            }


            void DestroyPressProxy(IDestroyable d)
            {
                pressProxy.Value.Changed -= CherryPickButtonPress;
                pressProxy.Destroyed -= DestroyPressProxy;
            }


            pressProxy.Value.Changed += CherryPickButtonPress;
            pressProxy.Destroyed += DestroyPressProxy;
        }


        // Funny magic UI numbers
        var text = (Text)button.LabelTextField.Parent;
        text.Size.Value = 24.44582f;


        // Smooth the color transitions on the buttons for visual appeal
        var smooth = button.Slot.AttachComponent<SmoothValue<colorX>>();
        IField<colorX> target = button.ColorDrivers.First().ColorDrive.Target;
        smooth.TargetValue.Value = target.Value;


        button.ColorDrivers.First().ColorDrive.Target = smooth.TargetValue;
        smooth.Value.Target = target;
        smooth.Speed.Value = 12f;


        return button;
    }


    void OnOwnerDestroyed(IDestroyable destroyable)
    {
        _searchEditor?.LocalEditingChanged -= EditChanged;
        _owner?.Destroyed -= OnOwnerDestroyed;
    }
}



// When using this in a SortedList, you won't be able to look up an entry by key!!
public readonly struct MatchRatioComparer : IComparer<float>
{
    public readonly int Compare(float x, float y)
    {
        return x > y ? -1 : 1;
    }
}


// public static class CherryPickSearcher
// {
//     public void PerformMatch(string query, int resultCount = 10, bool showProtofluxComponents = true)
//     {
//         ResetResults(resultCount);

//         int workerCount = Workers.Length;
//         WorkerDetails[] details = Workers;

//         string[] splitQuery = query.Split(' ');



//         // The for loops are a bit hot and can cause minor
//         // hitches if care isn't taken. Avoiding branch logic if possible

//         // Check if there's actually anything to filter, because if there isn't, a slightly more efficient loop can be used
//         if (string.IsNullOrEmpty(Scope) && showProtofluxComponents)
//         {
//             for (int i = 0; i < workerCount; i++)
//             {
//                 WorkerDetails worker = details[i];
//                 float ratio = CherryPick_Helper.MatchRatioInsensitive(worker.LowerName, splitQuery);

//                 _results.Add(ratio, worker);
//                 int detailCount = _results.Count;

//                 _results.RemoveAt(detailCount - 1);
//             }
//         }
//         else
//         {
//             string searchScope = "/" + Scope;
//             for (int i = 0; i < workerCount; i++)
//             {
//                 WorkerDetails worker = details[i];
//                 float ratio = worker.Path.StartsWith(searchScope) && (showProtofluxComponents || !worker.Path.StartsWith(PROTOFLUX_PREFIX))
//                     ? CherryPick_Helper.MatchRatioInsensitive(worker.LowerName, splitQuery)
//                     : 0f;

//                 _results.Add(ratio, worker);
//                 int detailCount = _results.Count;

//                 _results.RemoveAt(detailCount - 1);
//             }
//         }


//         // Remove the zero-scored results after the fact. Avoids another conditional in the hot path above
//         while (MathX.Approximately(_results.LastOrDefault().Key, 0f) && _results.Count > 0)
//             _results.RemoveAt(_results.Count - 1);

//     }
// }