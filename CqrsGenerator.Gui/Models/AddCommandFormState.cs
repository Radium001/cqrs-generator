using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record AddCommandFormState(
    string? FeatureName,
    string? FeaturePath,
    string CommandName,
    string? ResponseType,
    IReadOnlyList<PropertySpec> Properties,
    IReadOnlyList<CommandHandlerDependency> Dependencies,
    IReadOnlyList<object> RepositoryDrafts,
    bool UpdateWebImports);
