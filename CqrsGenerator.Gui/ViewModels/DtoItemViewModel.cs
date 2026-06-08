namespace CqrsGenerator.Gui.ViewModels;

public sealed class DtoItemViewModel
{
    public DtoItemViewModel(string name, string displayName, string namespaceName)
    {
        Name = name;
        DisplayName = displayName;
        Namespace = namespaceName;
    }

    public string Name { get; }

    public string DisplayName { get; }

    public string Namespace { get; }

    public override string ToString() => DisplayName;
}
