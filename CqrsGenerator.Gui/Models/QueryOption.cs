using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record QueryOption(
    string Name,
    string ResultTypeName,
    ResponseShape Shape,
    bool IsGeneratedInSession);
