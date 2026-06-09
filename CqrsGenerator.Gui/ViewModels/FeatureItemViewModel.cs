using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class FeatureItemViewModel
{
    public FeatureItemViewModel(string name, string relativePath, bool isRuntime = false)
    {
        Name = name;
        RelativePath = relativePath;
        IsRuntime = isRuntime;
    }

    public FeatureItemViewModel(ArtifactRef reference)
    {
        Ref = reference;
        Name = reference.DisplayName ?? reference.Name;
        RelativePath = reference.FeaturePath ?? string.Empty;
        IsRuntime = reference.IsFromSession;
    }

    public ArtifactRef? Ref { get; }

    public string Name { get; }

    public string RelativePath { get; }

    public bool IsRuntime { get; }

    public bool IsFromSession => Ref?.IsFromSession ?? IsRuntime;

    public Guid? NodeId => Ref?.NodeId;

    public override string ToString() => Name;
}
