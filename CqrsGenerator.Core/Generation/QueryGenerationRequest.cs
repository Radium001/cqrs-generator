namespace CqrsGenerator.Core.Generation;

public sealed class QueryGenerationRequest
{
    public required string FeaturePath { get; init; }

    public required string QueryName { get; init; }

    public required string DtoName { get; init; }

    public string DtoNamespace { get; init; } = string.Empty;

    public required ResponseShape ResponseShape { get; init; }

    public IReadOnlyList<PropertySpec> Properties { get; init; } = [];

    public string? ServiceInterfaceName { get; init; }

    public string? ServiceMethodName { get; init; }

    public bool CreateDto { get; init; }

    public bool GenerateHandlerBody { get; init; }
}
