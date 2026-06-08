namespace CqrsGenerator.Core.Generation;

public sealed class FeatureStructureGenerationRequest
{
    public required string FeaturePath { get; init; }
    public bool CreateWebFeature { get; init; } = true;
}