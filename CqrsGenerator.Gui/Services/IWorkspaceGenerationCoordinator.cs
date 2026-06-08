using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services;

public interface IWorkspaceGenerationCoordinator
{
    Task<WorkspacePlanBuildResult> BuildPlanAsync(
        ProjectWorkspaceContext projectWorkspaceContext,
        IRootGeneratorSessionViewModel? rootSession,
        CancellationToken cancellationToken);
}
