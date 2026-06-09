namespace CqrsGenerator.Gui.Session;

public sealed record ArtifactRef(
    Guid NodeId,
    GeneratorNodeKind Kind,
    string Name);
