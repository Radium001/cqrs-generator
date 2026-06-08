using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record WebPageQueryBindingState(
    string QueryName,
    string ResultTypeName,
    string Args,
    ResponseShape ResponseShape,
    bool HasRefresh);
