namespace CqrsGenerator.Core.Generation;

public sealed class WebPageGenerationRequest
{
    public required string WebFeaturePath { get; init; }

    public required string PageName { get; init; }

    public required string Route { get; init; }

    public bool CreateImports { get; init; } = true;

    public IReadOnlyList<WebPageQueryBinding> Queries { get; init; } = [];
}
