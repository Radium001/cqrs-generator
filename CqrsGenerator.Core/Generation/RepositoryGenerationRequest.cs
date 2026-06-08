namespace CqrsGenerator.Core.Generation;

public sealed class RepositoryGenerationRequest
{
    public required string EntityName { get; init; }

    public string? Namespace { get; init; }

    public string? EntityNamespace { get; init; }

    public bool AddDependencyInjectionRegistration { get; init; } = true;

    public IReadOnlyList<RepositoryMethodSpec> Methods { get; init; } = [];
}

public sealed record RepositoryMethodSpec(string Name, string ReturnType, IReadOnlyList<PropertySpec> Parameters);
