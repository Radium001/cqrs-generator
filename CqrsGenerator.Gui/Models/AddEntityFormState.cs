using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record AddEntityFormState(
    EntitySourceMode SourceMode,
    EfEntityCandidate? SelectedEfEntity,
    IReadOnlyList<string> SelectedEfPropertyNames,
    bool RenameEfIdentifierProperties,
    string EntityName,
    string? Subfolder,
    IReadOnlyList<PropertySpec> ManualProperties,
    bool GenerateFactoryMethod,
    bool GenerateEfMapping,
    IReadOnlyList<string> DomainMethods);
