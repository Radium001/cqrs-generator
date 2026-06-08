namespace CqrsGenerator.Core.Generation;

public sealed class LocalQueryDtoGenerationRequest
{
    public required string FeaturePath { get; init; }

    public required string QueryName { get; init; }

    public required string DtoName { get; init; }

    public IReadOnlyList<PropertySpec> Properties { get; init; } = [];
}
