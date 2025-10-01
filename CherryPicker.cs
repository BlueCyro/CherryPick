using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;
using static CherryPick.CherryPick;

namespace CherryPick;

public class CherryPicker(Slot searchRoot, Slot componentUIRoot, ButtonEventHandler<string> onGenericPressed, ButtonEventHandler<string> onAddPressed, UIBuilder searchBuilder, Sync<string> scope)
{
    private static readonly string PROTOFLUX_PREFIX = "/ProtoFlux/Runtimes/";
    public string Scope
    {
        get => scope.Value;
        set => scope.Value = value;
    }
    public static bool IsReady { get; private set; }
    public static WorkerDetails[] Workers => _allWorkers;
    private readonly SortedList<float, WorkerDetails> _results = new(new MatchRatioComparer()); // Not queryable by index due to the implementation of MatchRatioComparer
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

        foreach (var category in allCategories)
        {
            foreach (var element in category.Elements)
            {
                if (element.IsDataModelType())
                {
                    string elementName = element.GetNiceName();
                    string elementPath = category.GetPath();

                    // writer.WriteLine($"{elementName}\n{elementPath}\n");
                    WorkerDetails detail = new(elementName, elementPath, element);
                    details.Add(detail);
                }
            }
        }

        _allWorkers = [.. details];
    }



    public static void SetReady() => IsReady = true;



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



    #region TextEditor Events



    // public void EditStart(TextEditor editor)
    // {
    //     if (componentUIRoot != null && searchRoot != null)
    //     {
    //         componentUIRoot.ActiveSelf = false;
    //         searchRoot.ActiveSelf = true;
    //     }
    // }



    // public void EditFinished(TextEditor editor)
    // {
    //     if (editor != null &&
    //         editor.Text.Target != null &&
    //         string.IsNullOrEmpty(editor.Text.Target.Text) &&
    //         componentUIRoot != null &&
    //         searchRoot != null)
    //     {
    //         componentUIRoot.ActiveSelf = true;
    //         searchRoot.ActiveSelf = false;
    //     }
    // }



    public void OpenPickerView()
    {
        componentUIRoot.ActiveSelf = false;
        searchRoot.ActiveSelf = true;
    }

    public void ClosePickerView()
    {
        componentUIRoot.ActiveSelf = true;
        searchRoot.ActiveSelf = false;
    }

    public static void ClearSearch(TextEditor editor)
    {
        editor.Text.Target.Text = null!;
    }



    public void EditChanged(TextEditor editor)
    {
        if (searchRoot == null ||
            componentUIRoot == null || 
            editor == null ||
            onGenericPressed == null ||
            onAddPressed == null || 
            searchBuilder == null ||
            !IsReady) // You can't search until the cache is built! This is fine in most cases, but if you end up searching before then, too bad!
                return;


        string txt = editor.Text.Target.Text;
        if (string.IsNullOrEmpty(txt))
        {
            ClosePickerView();
            return;
        }

        OpenPickerView();


        int genericStart = txt.IndexOf('<');
        int genericEnd = txt.LastIndexOf('>');
        int diff = (genericEnd - genericStart) - 1;
        string? matchTxt = null;
        string? genericType = null;

        if (genericStart > 0)
        {
            matchTxt = txt.Substring(0, genericStart);

            if (genericEnd > 0 && diff > 0)
                genericType = txt.Substring(genericStart + 1, diff);
        }
        else
        {
            matchTxt = txt;
        }


        // searchRoot.DestroyChildren();
        int resultCount = Config!.GetValue(ResultCount);
        resultCount = MathX.Min(resultCount, MAX_RESULT_COUNT);

        // Three different possibilities:
        // 1. ProtoFlux Search (Scope is non-empty): Show ProtoFlux (obviously)
        // 2. Component Search (Scope is empty), ProtoFlux is hidden: Don't show ProtoFlux
        // 3. Component Search (Scope is empty), ProtoFlux should be shown (debug mode?): Show ProtoFlux
        bool showProtofluxComponents = !string.IsNullOrEmpty(Scope) || Config!.GetValue(ShowProtofluxInComponentSearch);

        PerformMatch(matchTxt, resultCount, showProtofluxComponents);

        try
        {
            searchRoot.FindChild(s => s.OrderOffset == -1024)?.Destroy();
            WorkerDetails firstGeneric = _results.Values.First(w => w.Type.IsGenericTypeDefinition); // Bails the try/catch if it fails

            if (!string.IsNullOrEmpty(genericType))
            {
                Type? genParam = searchRoot.World.Types.ParseNiceType(genericType, true);
                if (genParam is null)
                    goto PARSE_TYPES;

                Type? constructed = firstGeneric.Type.MakeGenericType(genParam);

                if (constructed is null)
                    goto PARSE_TYPES;

                bool isValid = (bool?)constructed.GetProperty("IsValidGenericType")?.GetValue(null) ?? true;

                if (!isValid)
                    goto PARSE_TYPES;

                try
                {
                    string arg = searchRoot.World.Types().EncodeType(constructed);
                    WorkerDetails detail = new(constructed.GetNiceName(), firstGeneric.Path, constructed);
                    Button typeButton = CreateButton(detail, onAddPressed, arg, searchBuilder, editor, RadiantUI_Constants.Sub.ORANGE);
                    typeButton.Slot.OrderOffset = -1024;
                }
                catch (ArgumentException)
                {
                    CherryPick.Warn($"Tried to encode a non-data model type: {constructed}");
                }
            }
        }
        catch (InvalidOperationException) { } // Swallow this exception in particular because First() will throw if nothing satisfies the lambda condition

        PARSE_TYPES:

        for (int i = 0; i < searchRoot.ChildrenCount; i++)
        {
            if (searchRoot[i].OrderOffset == -1024)
                continue;

            if (!_results.Any(r => r.Value.Name == searchRoot[i].Name))
            {
                searchRoot[i].ActiveSelf = false;
                searchRoot.World.RunInUpdates(1, searchRoot[i].Destroy);
            }
        }

        int j = 0;
        foreach (var result in _results.Values)
        {
            bool isGenType = result.Type.IsGenericTypeDefinition;
            string arg = "";

            Slot? existingMatch = searchRoot.FindChild(s => s.Name == result.Name);
            if (existingMatch is not null)
            {
                existingMatch.OrderOffset = j++;
                continue;
            }

            Slot? buttonSlot = null;
            try
            {
                arg = isGenType ? Path.Combine(result.Path, result.Type.AssemblyQualifiedName) : searchRoot.World.Types().EncodeType(result.Type);
                var pressed = isGenType ? onGenericPressed : onAddPressed;
                buttonSlot = CreateButton(result, pressed, arg, searchBuilder, editor, RadiantUI_Constants.Sub.CYAN).Slot;
            }
            catch (ArgumentException)
            {
                CherryPick.Warn($"Tried to encode a non-data model type: {result.Type}");
            }
            j++;
        }
    }



    #endregion



    // One day we will have better UI construction... one day :')
    private Button CreateButton(in WorkerDetails detail, ButtonEventHandler<string> pressed, string arg, UIBuilder builder, TextEditor editor, colorX col)
    {
        // Snip the scope off of the beginning of the path if the browser so that it's relative to the scope
        string path = Scope != null ? detail.Path.Replace("/" + Scope, null) : detail.Path;
        string buttonText = $"<noparse={detail.Name.Length}>{detail.Name}<br><size=61.803%><line-height=133%>{path}";


        var button = builder.Button(buttonText, col, pressed, arg, PressDelay);
        ValueField<ulong> pressProxy = button.Slot.AddSlot("PressProxy").AttachComponent<ValueField<ulong>>();
        ButtonValueShift<ulong> proxyShifter = button.Slot.AttachComponent<ButtonValueShift<ulong>>();
        button.Slot.Name = detail.Name;

        proxyShifter.Delta.Value = 1;
        proxyShifter.TargetValue.Target = pressProxy.Value;

        ValueField<double> lastPressed = button.Slot.AddSlot("LastPressed").AttachComponent<ValueField<double>>();
        button.ClearFocusOnPress.Value = Config!.GetValue(CherryPick.ClearFocus);

        if (detail.Type.IsGenericTypeDefinition)
        {
            // Define delegate here for unsubscription later
            void CherryPickButtonPress(IChangeable c)
            {
                double now = searchRoot.World.Time.WorldTime;


                if (now - lastPressed.Value < CherryPick.PressDelay || CherryPick.SingleClick)
                {
                    ClosePickerView();
                    ClearSearch(editor);
                }
                else
                    lastPressed.Value.Value = now;
            }


            // // Destroy delegate for unsubscription
            // void ButtonDestroyed(IDestroyable d)
            // {
            //     IButton destroyedButton = (IButton)d;

            //     // When the button is destroyed, unsubscribe the events like a good boy
            //     destroyedButton.LocalPressed -= CherryPickButtonPress;
            //     destroyedButton.Destroyed -= ButtonDestroyed;
            // }

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
}



// When using this in a SortedList, you won't be able to look up an entry by key!!
public readonly struct MatchRatioComparer : IComparer<float>
{
    public readonly int Compare(float x, float y)
    {
        return x > y ? -1 : 1;
    }
}

