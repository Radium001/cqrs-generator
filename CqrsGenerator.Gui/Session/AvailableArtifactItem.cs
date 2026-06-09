namespace CqrsGenerator.Gui.Session;

public sealed record AvailableArtifactItem(
    string Name,
    GeneratorNodeKind Kind,
    Guid? NodeId,
    bool IsFromSession,
    bool IsFromProject,
    string? FeaturePath = null);
