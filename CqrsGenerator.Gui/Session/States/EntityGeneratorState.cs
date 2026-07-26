using System.Collections.ObjectModel;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Session.States;

public sealed class EntityGeneratorState
{
    public EntitySourceMode SourceMode { get; set; } = EntitySourceMode.Manual;

    public EfEntityCandidate? SelectedEfEntity { get; set; }

    public ObservableCollection<string> SelectedEfPropertyNames { get; } = new();

    public bool RenameEfIdentifierProperties { get; set; } = true;

    public string EntityName { get; set; } = string.Empty;

    public string? Subfolder { get; set; }

    public ObservableCollection<PropertySpec> Properties { get; } = new();

    public bool GenerateFactoryMethod { get; set; }

    public bool GenerateEfMapping { get; set; }

    public ObservableCollection<string> DomainMethods { get; } = new();
}
