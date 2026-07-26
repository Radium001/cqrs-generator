using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class FeatureItemViewModel
{
    public FeatureItemViewModel(ArtifactRef reference)
    {
        Ref = reference;
        Name = reference.DisplayName ?? reference.Name;
        RelativePath = reference.FeaturePath ?? string.Empty;
    }

    public ArtifactRef Ref { get; }

    public string Name { get; }

    public string RelativePath { get; }

    public bool IsFromSession => Ref.IsFromSession;

    public Guid? NodeId => Ref.NodeId;

    public override string ToString() => Name;
}
