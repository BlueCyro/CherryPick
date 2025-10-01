namespace CherryPick;

public readonly struct WorkerDetails(string name, string? path, Type type)
{
    public readonly string Name => name;
    public readonly string Path => path ?? "Uncategorized";
    public readonly Type Type => type;
    public readonly string? LowerName = name?.ToLower();
}
