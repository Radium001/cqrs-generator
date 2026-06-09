using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class CommandGeneratorState
{
    public string FeaturePath { get; set; } = string.Empty;

    public string CommandName { get; set; } = string.Empty;

    public string? ResponseType { get; set; }

    public ObservableCollection<PropertySpec> Parameters { get; } = new();

    public ObservableCollection<Guid> RepositoryNodeIds { get; } = new();
}
