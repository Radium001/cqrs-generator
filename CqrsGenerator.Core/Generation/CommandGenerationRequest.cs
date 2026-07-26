namespace CqrsGenerator.Core.Generation;

public sealed class CommandGenerationRequest
{
    public required string FeaturePath { get; init; }

    public required string CommandName { get; init; }

    public IReadOnlyList<PropertySpec> Properties { get; init; } = [];

    public IReadOnlyList<CommandHandlerDependency> Dependencies { get; init; } = [];

    public bool GenerateHandlerBody { get; init; } = true;

    public CommandHandlerScaffoldContext? HandlerScaffoldContext { get; init; }
}

public sealed record CommandHandlerDependency(string Type, string Name);

public sealed record CommandHandlerMethodContract(
    string Name,
    string ReturnType,
    IReadOnlyList<PropertySpec> Parameters,
    bool IsStatic = false);

public sealed record CommandHandlerEntityContract(
    string Name,
    string Namespace,
    IReadOnlyList<CommandHandlerMethodContract> Methods);

public sealed record CommandHandlerRepositoryContract(
    string InterfaceType,
    string DependencyName,
    string? EntityType,
    IReadOnlyList<CommandHandlerMethodContract> Methods,
    CommandHandlerEntityContract? Entity);

public sealed record CommandHandlerScaffoldContext(
    IReadOnlyList<CommandHandlerRepositoryContract> Repositories);
