namespace CqrsGenerator.Core.Generation;

public sealed class CommandGenerationRequest
{
    public required string FeaturePath { get; init; }

    public required string CommandName { get; init; }

    public string? ResponseType { get; init; }

    public IReadOnlyList<PropertySpec> Properties { get; init; } = [];

    public IReadOnlyList<CommandHandlerDependency> Dependencies { get; init; } = [];
}

public sealed record CommandHandlerDependency(string Type, string Name);
