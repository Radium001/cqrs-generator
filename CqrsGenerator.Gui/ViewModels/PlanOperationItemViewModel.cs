namespace CqrsGenerator.Gui.ViewModels;

public sealed class PlanOperationItemViewModel
{
    public PlanOperationItemViewModel(string title, string path, string details)
    {
        Title = title;
        Path = path;
        Details = details;
    }

    public string Title { get; }

    public string Path { get; }

    public string Details { get; }

    public string DisplayText => $"{Title}: {Path}";
}
