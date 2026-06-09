using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class DtoGeneratorState
{
    public string BaseName { get; set; } = string.Empty;

    public int SuffixIndex { get; set; }

    public ObservableCollection<PropertySpec> Properties { get; } = new();
}
