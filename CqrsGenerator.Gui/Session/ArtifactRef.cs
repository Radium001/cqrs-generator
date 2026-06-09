namespace CqrsGenerator.Gui.Session;

public sealed record ArtifactRef(
    GeneratorNodeKind Kind,
    ArtifactOrigin Origin,
    string Name,
    Guid? NodeId = null,
    string? FeaturePath = null,
    string? ProjectPath = null,
    string? Namespace = null,
    string? OwnerName = null,
    string? DisplayName = null)
{
    public bool IsFromSession => Origin == ArtifactOrigin.Session;

    public bool IsFromProject => Origin == ArtifactOrigin.Project;
}
