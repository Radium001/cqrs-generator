using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record NewDtoDraft(
    string BaseName,
    int SuffixIndex,
    IReadOnlyList<PropertySpec> Properties);
