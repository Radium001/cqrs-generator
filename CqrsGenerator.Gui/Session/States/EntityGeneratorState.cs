using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class EntityGeneratorState
{
    public string EntityName { get; set; } = string.Empty;

    public ObservableCollection<PropertySpec> Properties { get; } = new();
}
