using System.Collections.ObjectModel;

namespace CqrsGenerator.Gui.Session.States;

public sealed class WebPageGeneratorState
{
    public string FeaturePath { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public ObservableCollection<Guid> QueryNodeIds { get; } = new();
}
