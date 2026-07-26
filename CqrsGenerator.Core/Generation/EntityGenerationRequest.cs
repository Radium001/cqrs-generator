namespace CqrsGenerator.Core.Generation;

public sealed class EntityGenerationRequest
{
    public required string EntityName { get; init; }

    public string? Namespace { get; init; }

    public string? SubFolder { get; init; }

    public IReadOnlyList<PropertySpec> Properties { get; init; } = [];

    public bool GenerateFactoryMethod { get; init; }

    public IReadOnlyList<string> DomainMethods { get; init; } = [];

    public bool GenerateEfMapping { get; init; }

    public IReadOnlyList<(string DomainName, string EfName)>? EfMappingFields { get; init; }
}
