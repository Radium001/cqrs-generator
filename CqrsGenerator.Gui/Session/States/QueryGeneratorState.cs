using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class QueryGeneratorState
{
    public string FeaturePath { get; set; } = string.Empty;

    public string QueryName { get; set; } = string.Empty;

    public Guid? ResultDtoNodeId { get; set; }

    public string? ExistingResultDtoName { get; set; }

    public ObservableCollection<PropertySpec> Parameters { get; } = new();
}
