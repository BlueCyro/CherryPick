using System.Diagnostics.CodeAnalysis;
using Elements.Core;

namespace CherryPick;

public readonly struct WorkerDetails
{
    public readonly bool IsEmptyGeneric => Type.IsGenericTypeDefinition;
    public readonly bool IsQualifiedGeneric => Type.IsConstructedGenericType;

    public readonly Type Type
    {
        get => _type;
        init => Setup(value, Path, out _type, out Name, out _path, out NiceName, out LowerName, out AssemblyQualifiedName);
    }
    readonly Type _type;

    public readonly string Name;
    public readonly string Path { get => _path; init => _path = value; }
    readonly string _path;

    public readonly string NiceName;
    public readonly string LowerName;
    public readonly string AssemblyQualifiedName;



    public WorkerDetails(Type type, string? path) => Setup(type, path, out _type, out Name, out _path, out NiceName, out LowerName, out AssemblyQualifiedName);

    static void Setup(
        Type detailType,
        string? detailPath,
        out Type type,
        out string name,
        out string path,
        out string niceName,
        out string lowerName,
        out string qualifiedName)
    {
        type = detailType;
        name = type.GetBareName();
        path = detailPath ?? "Uncategorized";
        niceName = type.GetNiceName();
        lowerName = name.ToLower();
        qualifiedName = type.AssemblyQualifiedName ?? throw new InvalidOperationException($"Can only construct {nameof(WorkerDetails)} with types from valid assemblies");
    }
}
