using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Cli.Interactive;

public sealed record InteractiveMenuAction(
    string Label,
    Action<ProjectModel> Execute);
