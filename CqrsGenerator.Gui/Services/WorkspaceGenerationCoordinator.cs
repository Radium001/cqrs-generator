using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services;

public sealed class WorkspaceGenerationCoordinator : IWorkspaceGenerationCoordinator
{
    private readonly IPlanPreviewService _planPreviewService;
    private readonly PlanPreparationService _planPreparationService;

    public WorkspaceGenerationCoordinator(IPlanPreviewService planPreviewService, PlanPreparationService planPreparationService)
    {
        _planPreviewService = planPreviewService;
        _planPreparationService = planPreparationService;
    }

    public Task<WorkspacePlanBuildResult> BuildPlanAsync(
        ProjectWorkspaceContext projectWorkspaceContext,
        IRootGeneratorSessionViewModel? rootSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (rootSession is not IPlanBuildingRootSessionViewModel planBuildingSession)
        {
            return Task.FromResult(WorkspacePlanBuildResult.Failure(
                "Build plan failed: no generator action is active.",
                "No generator action is active.",
                null,
                PlanPreviewSnapshot.Error("Select a generator action before building a plan.")));
        }

        if (!planBuildingSession.CanBuildPlan)
        {
            return Task.FromResult(WorkspacePlanBuildResult.Failure(
                "Build plan failed: the current generator form is incomplete.",
                "The current generator form is incomplete.",
                null,
                PlanPreviewSnapshot.Error("Complete the generator form to build a plan.")));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = planBuildingSession.BuildPlan(projectWorkspaceContext);
            var preparedPackage = _planPreparationService.Prepare(plan, projectWorkspaceContext.TargetRootPath);
            var snapshot = _planPreviewService.Build(preparedPackage);
            return Task.FromResult(WorkspacePlanBuildResult.Success("Plan built.", plan, preparedPackage, snapshot));
        }
        catch (Exception ex)
        {
            return Task.FromResult(WorkspacePlanBuildResult.Failure(
                $"Build plan failed: {ex.Message}",
                ex.Message,
                ex.ToString(),
                PlanPreviewSnapshot.Error("Plan build failed. Review the error message and try again.")));
        }
    }
}
