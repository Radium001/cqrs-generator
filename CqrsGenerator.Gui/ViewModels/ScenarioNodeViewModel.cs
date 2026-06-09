using System.Collections.ObjectModel;
using CqrsGenerator.Gui.Session;

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

    public ScenarioNodeViewModel(GeneratorNode node)
    {
        Title = GetTitle(node.Kind);
        Status = MapStatus(node.Status);
        Summary = node.Title;

        Children = new ObservableCollection<ScenarioNodeViewModel>(
            node.Children.Select(c => new ScenarioNodeViewModel(c)));
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

    private static ScenarioNodeStatus MapStatus(GeneratorNodeStatus status) => status switch
    {
        GeneratorNodeStatus.Ready => ScenarioNodeStatus.Ready,
        GeneratorNodeStatus.Valid => ScenarioNodeStatus.Ready,
        GeneratorNodeStatus.Invalid => ScenarioNodeStatus.Invalid,
        GeneratorNodeStatus.Conflict => ScenarioNodeStatus.Invalid,
        GeneratorNodeStatus.Draft => ScenarioNodeStatus.Draft,
        _ => ScenarioNodeStatus.Missing,
    };

    private static string GetTitle(GeneratorNodeKind kind) => kind switch
    {
        GeneratorNodeKind.Feature => "Feature",
        GeneratorNodeKind.Dto => "DTO",
        GeneratorNodeKind.Query => "Query",
        GeneratorNodeKind.Command => "Command",
        GeneratorNodeKind.Repository => "Repository",
        GeneratorNodeKind.Entity => "Entity",
        GeneratorNodeKind.WebPage => "Web Page",
        _ => kind.ToString(),
    };
}
