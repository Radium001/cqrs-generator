namespace CqrsGenerator.Gui.Session;

public sealed record AvailableArtifactItem(
    ArtifactRef Ref,
    string DisplayName,
    string? SecondaryText = null,
    bool IsSelectable = true,
    string? SelectionBlockedReason = null)
{
    public string Name => Ref.Name;

    public GeneratorNodeKind Kind => Ref.Kind;

    public Guid? NodeId => Ref.NodeId;

    public bool IsFromSession => Ref.Origin == ArtifactOrigin.Session;

    public bool IsFromProject => Ref.Origin == ArtifactOrigin.Project;

    public string? FeaturePath => Ref.FeaturePath;
}
