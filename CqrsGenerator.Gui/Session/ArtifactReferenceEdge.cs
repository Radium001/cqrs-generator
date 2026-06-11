namespace CqrsGenerator.Gui.Session;

public sealed record ArtifactReferenceEdge(
    Guid SourceNodeId,
    string Relationship,
    ArtifactRef Target,
    bool IsOwnership = false);
