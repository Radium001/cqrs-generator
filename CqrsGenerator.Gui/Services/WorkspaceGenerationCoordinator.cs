using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services;

public sealed class WorkspaceGenerationCoordinator : IWorkspaceGenerationCoordinator
{
    private readonly GenerationSession _generationSession;
    private readonly GenerationSessionValidationService _validationService;
    private readonly GenerationSessionPlanBuilder _planBuilder;
    private readonly IPlanPreviewService _planPreviewService;
    private readonly PlanPreparationService _planPreparationService;

    public WorkspaceGenerationCoordinator(
        GenerationSession generationSession,
        GenerationSessionValidationService validationService,
        GenerationSessionPlanBuilder planBuilder,
        IPlanPreviewService planPreviewService,
        PlanPreparationService planPreparationService)
    {
        _generationSession = generationSession;
        _validationService = validationService;
        _planBuilder = planBuilder;
        _planPreviewService = planPreviewService;
        _planPreparationService = planPreparationService;
    }

    public Task<WorkspacePlanBuildResult> BuildPlanAsync(
        ProjectWorkspaceContext projectWorkspaceContext,
        IRootGeneratorSessionViewModel? rootSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (rootSession is not IGeneratorNodeEditorViewModel { Node: not null } rootEditor)
        {
            return Task.FromResult(WorkspacePlanBuildResult.Failure(
                "Build plan failed: no generator action is active.",
                "No generator action is active.",
                null,
                PlanPreviewSnapshot.Error("Select a generator action before building a plan.")));
        }

        if (_generationSession.Roots.Count == 0)
        {
            return Task.FromResult(WorkspacePlanBuildResult.Failure(
                "Build plan failed: the session graph is empty.",
                "The session graph is empty.",
                null,
                PlanPreviewSnapshot.Error("Create or select a scenario before building a plan.")));
        }

        if (_generationSession.FindNode(rootEditor.Node!.Id) is null)
        {
            return Task.FromResult(WorkspacePlanBuildResult.Failure(
                "Build plan failed: the active scenario is detached from the session graph.",
                "The active scenario is detached from the session graph.",
                null,
                PlanPreviewSnapshot.Error("Reopen the scenario and try building again.")));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _generationSession.Artifacts.SetProjectModel(projectWorkspaceContext.ProjectModel);

            var validation = _validationService.Validate(_generationSession);
            if (!validation.IsValid)
            {
                var errorMessage = string.Join(Environment.NewLine, validation.Errors);
                var details = validation.Warnings.Count == 0
                    ? errorMessage
                    : $"{errorMessage}{Environment.NewLine}{Environment.NewLine}Warnings:{Environment.NewLine}{string.Join(Environment.NewLine, validation.Warnings)}";
                return Task.FromResult(WorkspacePlanBuildResult.Failure(
                    "Build plan failed: the session graph is invalid.",
                    errorMessage,
                    details,
                    PlanPreviewSnapshot.Error("Fix invalid scenario nodes before building a plan.")));
            }

            var plan = _planBuilder.BuildPlan(
                _generationSession,
                new Session.CoreWorkflowContext(
                    projectWorkspaceContext,
                    projectWorkspaceContext.Config,
                    projectWorkspaceContext.ProjectModel,
                    EmptyServiceProvider.Instance));
            foreach (var warning in validation.Warnings)
            {
                plan.AddWarning(warning);
            }
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

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }
}
