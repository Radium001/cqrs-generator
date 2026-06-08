using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record NewEntityDraft(
    EntitySourceMode SourceMode,
    string EntityName,
    string? SelectedEfEntityName,
    IReadOnlyList<string> SelectedEfPropertyNames,
    bool RenameEfIdentifierProperties,
    string? Subfolder,
    IReadOnlyList<PropertySpec> ManualProperties,
    IReadOnlyList<PropertySpec> Properties,
    bool GenerateFactoryMethod,
    bool GenerateEfMapping,
    bool GenerateInterface,
    IReadOnlyList<string> DomainMethods,
    IReadOnlyList<(string DomainName, string EfName)>? EfMappingFields);
