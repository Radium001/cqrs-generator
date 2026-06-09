using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.Session;

public sealed record CoreWorkflowContext(
    ProjectWorkspaceContext WorkspaceContext,
    GeneratorConfig Config,
    ProjectModel ProjectModel,
    IServiceProvider ServiceProvider);
