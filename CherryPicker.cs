using System.Reflection;
using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;
using static CherryPick.CherryPick;
using System.Runtime.CompilerServices;
using System;

namespace CherryPick;

public class CherryPicker(Slot searchRoot, Slot componentUIRoot, ButtonEventHandler<string> onGenericPressed, ButtonEventHandler<string> onAddPressed, UIBuilder searchBuilder, Sync<string> scope)
{
    public string Scope
    {
        get => scope.Value;
        set => scope.Value = value;
    }
    public static bool IsReady { get; private set; }
    public WorkerDetails[] Workers => _allWorkers;
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


        foreach (var category in allCategories)
        {
            foreach (var element in category.Elements)
            {
                if (element.IsDataModelType())
                {
                    WorkerDetails detail = new(element.GetNiceName(), category.GetPath(), element);
                    details.Add(detail);
                }
            }
        }

        _allWorkers = [.. details];
    }



    public static void SetReady() => IsReady = true;



    #region String matching



    public void PerformMatch(string query, int resultCount = 10)
    {
        ResetResults(resultCount);

        int workerCount = Workers.Length;
        WorkerDetails[] details = Workers;

        string[] splitQuery = query.Split(' ');

        string userexcludedcategories = Config!.GetValue(CherryPick.UserExcludedCategories).ToLower();
        string[] UserExcludedCategories = userexcludedcategories.Replace(" ", null).Split(',');


        // The for loops are a bit hot and can cause minor
        // hitches if care isn't taken. Avoiding branch logic if possible

        // Check if there's actually a scope, because if there isn't, a slightly more efficient loop can be used
        if (string.IsNullOrEmpty(Scope))
        {
            for (int i = 0; i < workerCount; i++)
            {
                WorkerDetails worker = details[i];
                if (!UserExcludedCategories.Any(worker.Path.ToLower().Contains) || userexcludedcategories == "")
                {
                    float ratio = MatchRatioInsensitive(worker.LowerName, splitQuery);

                    _results.Add(ratio, worker);
                    int detailCount = _results.Count;

                    _results.RemoveAt(detailCount - 1);
                }
            }
        }
        else
        {
            string searchScope = "/" + Scope;
            for (int i = 0; i < workerCount; i++)
            {
                WorkerDetails worker = details[i];

                if (!UserExcludedCategories.Any(worker.Path.ToLower().Replace("/protoflux", "").Contains) || userexcludedcategories == "")
                {
                    float ratio = worker.Path.StartsWith(searchScope) ? MatchRatioInsensitive(worker.LowerName, splitQuery) : 0f;

                    _results.Add(ratio, worker);
                    int detailCount = _results.Count;

                    _results.RemoveAt(detailCount - 1);
                }
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



    // Out of the total string length, how many characters actually match the query. Gives decent results.
    static float MatchRatioInsensitive(string? source, params string[] query)
    {
        if (source == null)
            return 0f;
        
        float totalScore = 0f;
        int indexFound = 1;


        for (int i = 0; i < query.Length; i++)
        {
            string item = query[i];
            int score = source.IndexOf(item, StringComparison.OrdinalIgnoreCase);


            // Nasty bit hack - faster way of getting the sign without any conditional branching. I hate this runtime
            indexFound *= IsPositive(score); // If this is ever zero, the score will remain zero


            // Sum the score, but make it zero if any query was not found
            totalScore = indexFound * (totalScore + item.Length / (source.Length + i + 1f));
        }


        return totalScore;
    }


    #endregion



    #region TextEditor Events



    public void EditStart(TextEditor editor)
    {
        if (componentUIRoot != null && searchRoot != null)
        {
            componentUIRoot.ActiveSelf = false;
            searchRoot.ActiveSelf = true;
        }
    }



    public void EditFinished(TextEditor editor)
    {         
        if (editor != null &&
            editor.Text.Target != null &&
            string.IsNullOrEmpty(editor.Text.Target.Text) &&
            componentUIRoot != null &&
            searchRoot != null)
        {
            componentUIRoot.ActiveSelf = true;
            searchRoot.ActiveSelf = false;
        }
    }



    public void ForceEditFinished(TextEditor editor)
    {
        editor.Text.Target.Text = null;
        EditFinished(editor);
    }

    
    int searchDelayToken = 0;

    public void EditChanged(TextEditor editor)
    {
        int SearchRefreshDelay = (int)(1000 * Config!.GetValue(CherryPick.SearchRefreshDelay));

        editor.StartTask(async () =>
        {
            searchDelayToken++;
            int LocalSearchDelayToken = searchDelayToken;

            if (SearchRefreshDelay > 0)
            {
                await default(ToBackground);
                await Task.Delay(SearchRefreshDelay);
                await default(NextUpdate);
            }

            // Only refresh UI with search results if there was no further update immediately following it
            if (searchDelayToken!= LocalSearchDelayToken)
                return;

            if (searchRoot == null ||
                componentUIRoot == null || 
                editor == null ||
                onGenericPressed == null ||
                onAddPressed == null || 
                searchBuilder == null ||
                !IsReady) // You can't search until the cache is built! This is fine in most cases, but if you end up searching before then, too bad!
                    return;

        
            string txt = editor.Text.Target.Text;
            if (txt == null)
                return;


            int genericStart = txt.IndexOf('<');
            string? matchTxt = null;
            string? genericType = null;

            if (genericStart > 0)
            {
                matchTxt = txt.Substring(0, genericStart);
                genericType = txt.Substring(genericStart);
            }
            else
            {
                matchTxt = txt;
            }


            searchRoot.DestroyChildren();
            int resultCount = Config!.GetValue(ResultCount);
            resultCount = MathX.Min(resultCount, MAX_RESULT_COUNT);


            PerformMatch(matchTxt, resultCount);
            foreach (var result in _results.Values)
            {
                bool isGenType = result.Type.IsGenericTypeDefinition;
                string arg = "";
            
                try
                {
                    arg = isGenType ? Path.Combine(result.Path, result.Type.AssemblyQualifiedName) : searchRoot.World.Types().EncodeType(result.Type);
                }
                catch (ArgumentException)
                {
                    CherryPick.Warn($"Tried to encode a non-data model type: {result.Type}");
                    continue;
                }

                var pressed = isGenType ? onGenericPressed : onAddPressed;
                CreateButton(result, pressed, arg, searchBuilder, editor, RadiantUI_Constants.Sub.CYAN);
            }


            try
            {
                WorkerDetails firstGeneric = _results.Values.First(w => w.Type.IsGenericTypeDefinition);
                if (genericType != null)
                {
                    string typeName = firstGeneric.Type.FullName;
                    typeName = typeName.Substring(0, typeName.IndexOf("`")) + genericType;
                    Type? constructed = NiceTypeParser.TryParse(typeName);


                    if (constructed != null)
                    {
                        try
                        {
                            string arg = searchRoot.World.Types().EncodeType(constructed);
                        }
                        catch (ArgumentException)
                        {
                            CherryPick.Warn($"Tried to encode a non-data model type: {constructed}");
                            return;
                        }

                        WorkerDetails detail = new(constructed.GetNiceName(), firstGeneric.Path, constructed);
                        Button typeButton = CreateButton(detail, onAddPressed, searchRoot.World.Types().EncodeType(constructed), searchBuilder, editor, RadiantUI_Constants.Sub.ORANGE);
                        typeButton.Slot.OrderOffset = -1024;
                    }
                }
            }
            catch (InvalidOperationException) { } // Swallow this exception in particular because First() will throw if nothing satisfies the lambda condition
        });
    }



    #endregion



    // One day we will have better UI construction... one day :')
    private Button CreateButton(in WorkerDetails detail, ButtonEventHandler<string> pressed, string arg, UIBuilder builder, TextEditor editor, colorX col)
    {
        // Snip the scope off of the beginning of the path if the browser so that it's relative to the scope
        string path = Scope != null ? detail.Path.Replace("/" + Scope, null) : detail.Path;
        string buttonText = $"<noparse={detail.Name.Length}>{detail.Name}<br><size=61.803%><line-height=133%>{path}";

        
        var button = builder.Button(buttonText, col, pressed, arg, CherryPick.PressDelay);
        ValueField<double> lastPressed = button.Slot.AddSlot("LastPressed").AttachComponent<ValueField<double>>();
        button.ClearFocusOnPress.Value = CherryPick.Config!.GetValue(CherryPick.ClearFocus);


        if (detail.Type.IsGenericTypeDefinition)
        {
            // Define delegate here for unsubscription later
            void CherryPickButtonPress(IButton b, ButtonEventData d)
            {
                double now = searchRoot.World.Time.WorldTime;


                if (now - lastPressed.Value < CherryPick.PressDelay || CherryPick.SingleClick)
                    ForceEditFinished(editor);
                else
                    lastPressed.Value.Value = now;
            }


            // Destroy delegate for unsubscription
            void ButtonDestroyed(IDestroyable d)
            {
                IButton destroyedButton = (IButton)d;
                
                // When the button is destroyed, unsubscribe the events like a good boy
                destroyedButton.LocalPressed -= CherryPickButtonPress;
                destroyedButton.Destroyed -= ButtonDestroyed;
            }


            button.LocalPressed += CherryPickButtonPress;
            button.Destroyed += ButtonDestroyed;
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


    
    /// <summary>
    /// Bitwise check to see if an integer is positive
    /// </summary>
    /// <param name="value">Integer to check</param>
    /// <returns>1 if positive, otherwise 0</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IsPositive(int value)
    {
        return 1 + ((value & int.MinValue) >> 31);
    }
}



// When using this in a SortedList, you won't be able to look up an entry by key!!
public struct MatchRatioComparer : IComparer<float>
{
    public readonly int Compare(float x, float y)
    {
        return x > y ? -1 : 1;
    }
}

