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

    public string Name { get; }

    public string DisplayName { get; }

    public string Namespace { get; }

    public string? RelativePath { get; }

    public override string ToString() => DisplayName;
}
