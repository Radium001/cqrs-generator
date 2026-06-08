namespace CqrsGenerator.Core.Generation;

public sealed record WebPageQueryBinding(
    string QueryName,
    string? VariableName,
    string Args,
    string ResultTypeName,
    ResponseShape ResponseShape,
    bool HasRefresh);
