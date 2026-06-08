namespace CqrsGenerator.Core.Generation;

public sealed class DtoGenerationRequest
{
    public required string FeaturePath { get; init; }

    public required string DtoName { get; init; }

    public string? Subfolder { get; init; }

    public IReadOnlyList<PropertySpec> Properties { get; init; } = [];
}
