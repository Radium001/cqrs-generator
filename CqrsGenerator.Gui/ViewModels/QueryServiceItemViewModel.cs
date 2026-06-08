namespace CqrsGenerator.Gui.ViewModels;

public sealed class QueryServiceItemViewModel
{
    public QueryServiceItemViewModel(
        string interfaceName,
        string implementationName,
        string interfacePath,
        string? implementationPath)
    {
        InterfaceName = interfaceName;
        ImplementationName = implementationName;
        InterfacePath = interfacePath;
        ImplementationPath = implementationPath;
    }

    public string InterfaceName { get; }

    public string ImplementationName { get; }

    public string InterfacePath { get; }

    public string? ImplementationPath { get; }

    public override string ToString() => InterfaceName;
}
