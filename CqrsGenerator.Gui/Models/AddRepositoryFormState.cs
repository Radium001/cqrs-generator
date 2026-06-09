using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record AddRepositoryFormState(
    string? ExistingEntityDisplayName,
    string? ExistingEntityName,
    string? ExistingEntityNamespace,
    object? CustomEntity,
    IReadOnlyList<string> SelectedMethodPresetKeys,
    IReadOnlyList<RepositoryMethodSpec> CustomMethods,
    bool AddDependencyInjectionRegistration);
