namespace CqrsGenerator.Gui.ViewModels;

public sealed class GeneratorBreadcrumbItem
{
    public GeneratorBreadcrumbItem(string title, bool isActive)
    {
        Title = title;
        IsActive = isActive;
    }

    public string Title { get; }

    public bool IsActive { get; }
}
