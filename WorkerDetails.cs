namespace CherryPick;

public struct WorkerDetails(string name, string? path, Type type)
{
    public readonly string Name => name;
    public readonly string Path => path ?? "";
    public readonly Type Type => type;
}
