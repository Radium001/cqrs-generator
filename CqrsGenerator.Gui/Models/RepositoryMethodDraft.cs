using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record RepositoryMethodDraft(
    string Name,
    string ReturnType,
    IReadOnlyList<PropertySpec> Parameters);
