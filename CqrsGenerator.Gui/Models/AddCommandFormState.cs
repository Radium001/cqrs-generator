using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record AddCommandFormState(
    string? FeatureName,
    string? FeaturePath,
    string CommandName,
    IReadOnlyList<PropertySpec> Properties,
    IReadOnlyList<CommandHandlerDependency> Dependencies,
    string? WebFeaturePath)
{
    public bool GenerateHandlerBody { get; init; } = true;

    public CommandHandlerScaffoldContext? HandlerScaffoldContext { get; init; }
}
