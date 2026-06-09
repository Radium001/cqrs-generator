using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class EntityItemViewModel
{
    public EntityItemViewModel(string name, string displayName, string namespaceName, string? relativePath)
    {
        Name = name;
        DisplayName = displayName;
        Namespace = namespaceName;
        RelativePath = relativePath;
    }

    public EntityItemViewModel(ArtifactRef reference)
    {
        Ref = reference;
        Name = reference.Name;
        DisplayName = reference.DisplayName ?? reference.Name;
        Namespace = reference.Namespace ?? "Domain.Entities";
        RelativePath = reference.FeaturePath;
    }

    public ArtifactRef? Ref { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string Namespace { get; }

    public string? RelativePath { get; }

    public bool IsFromSession => Ref?.IsFromSession == true;

    public Guid? NodeId => Ref?.NodeId;

    public override string ToString() => DisplayName;
}
