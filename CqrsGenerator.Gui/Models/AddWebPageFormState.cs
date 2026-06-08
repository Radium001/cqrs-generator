using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record AddWebPageFormState(
    string? WebFeaturePath,
    string? WebFeatureName,
    string? PageName,
    string? Route,
    bool CreateImports,
    IReadOnlyList<WebPageQueryBindingState> QueryBindings,
    IReadOnlyList<AddQueryFormState> QueryDrafts);
