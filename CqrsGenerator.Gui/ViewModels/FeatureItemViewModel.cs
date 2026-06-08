namespace CqrsGenerator.Gui.ViewModels;

public sealed class FeatureItemViewModel
{
    public FeatureItemViewModel(string name, string relativePath, bool isRuntime = false)
    {
        Name = name;
        RelativePath = relativePath;
        IsRuntime = isRuntime;
    }

    public string Name { get; }

    public string RelativePath { get; }

    public bool IsRuntime { get; }

    public override string ToString() => Name;
}
