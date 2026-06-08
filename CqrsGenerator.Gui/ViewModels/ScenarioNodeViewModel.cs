using System.Collections.ObjectModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class ScenarioNodeViewModel
{
    public ScenarioNodeViewModel(string title, ScenarioNodeStatus status, string summary)
    {
        Title = title;
        Status = status;
        Summary = summary;
        Children = [];
    }

    public string Title { get; }

    public ScenarioNodeStatus Status { get; }

    public string Summary { get; }

    public ObservableCollection<ScenarioNodeViewModel> Children { get; }

    public string StatusText => Status switch
    {
        ScenarioNodeStatus.Ready => "Ready",
        ScenarioNodeStatus.Invalid => "Invalid",
        ScenarioNodeStatus.Draft => "Draft",
        _ => "Missing",
    };
}
