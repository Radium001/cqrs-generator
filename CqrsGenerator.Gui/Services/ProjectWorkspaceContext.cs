using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Gui.Services;

public sealed record ProjectWorkspaceContext(
    string TargetRootPath,
    GeneratorConfig Config,
    ProjectModel ProjectModel);
