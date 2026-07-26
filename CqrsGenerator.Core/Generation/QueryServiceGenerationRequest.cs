namespace CqrsGenerator.Core.Generation;

public sealed class QueryServiceGenerationRequest
{
    public required string FeaturePath { get; init; }

    public required string InterfaceName { get; init; }

    public required string ImplementationName { get; init; }

    public required string ImplementationPath { get; init; }

    public required string ImplementationNamespace { get; init; }

    public bool AddDependencyInjectionRegistration { get; init; } = true;

    public bool GenerateImplementationBody { get; init; }

    public string? DtoTypeName { get; init; }

    public string DtoNamespace { get; init; } = string.Empty;

    public string? InitialReturnType { get; init; }

    public string? InitialMethodName { get; init; }

    public IReadOnlyList<PropertySpec> InitialParameters { get; init; } = [];
}

public sealed class QueryServiceMethodGenerationRequest
{
    public required string InterfacePath { get; init; }

    public required string ImplementationPath { get; init; }

    public required string InterfaceName { get; init; }

    public required string ImplementationName { get; init; }

    public required string ReturnType { get; init; }

    public required string MethodName { get; init; }

    public bool GenerateImplementationBody { get; init; }

    public string? DtoTypeName { get; init; }

    public string DtoNamespace { get; init; } = string.Empty;

    public IReadOnlyList<PropertySpec> Parameters { get; init; } = [];
}
