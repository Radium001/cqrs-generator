namespace CqrsGenerator.Core.Generation;

public sealed record RepositoryMethodPreset(string Key, string Name, bool IsSelectedByDefault);

public static class RepositoryMethodCatalog
{
    public static readonly RepositoryMethodPreset GetById = new("get-by-id", GeneratorConstants.RepoMethodGetById, true);
    public static readonly RepositoryMethodPreset Add = new("add", GeneratorConstants.RepoMethodAdd, true);
    public static readonly RepositoryMethodPreset Update = new("update", GeneratorConstants.RepoMethodUpdate, true);
    public static readonly RepositoryMethodPreset Delete = new("delete", GeneratorConstants.RepoMethodDelete, false);

    public static IReadOnlyList<RepositoryMethodPreset> Presets { get; } =
    [
        GetById,
        Add,
        Update,
        Delete,
    ];

    public static RepositoryMethodSpec Create(string key, string entityName)
    {
        return key switch
        {
            "get-by-id" => new RepositoryMethodSpec(GeneratorConstants.RepoMethodGetById, $"Task<{entityName}>", [new PropertySpec("int", GeneratorConstants.DefaultIdParamName)]),
            "add" => new RepositoryMethodSpec(GeneratorConstants.RepoMethodAdd, "Task", [new PropertySpec(entityName, GeneratorConstants.DefaultEntityParamName)]),
            "update" => new RepositoryMethodSpec(GeneratorConstants.RepoMethodUpdate, "Task", [new PropertySpec(entityName, GeneratorConstants.DefaultEntityParamName)]),
            "delete" => new RepositoryMethodSpec(GeneratorConstants.RepoMethodDelete, "Task", [new PropertySpec("int", GeneratorConstants.DefaultIdParamName)]),
            _ => throw new ArgumentException($"Unknown repository method preset '{key}'.", nameof(key)),
        };
    }
}
