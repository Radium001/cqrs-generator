using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services;

public sealed class WorkspaceSessionService : IWorkspaceSessionService
{
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IWorkspaceShellService _workspaceShellService;
    private readonly IWorkspaceGenerationCoordinator _workspaceGenerationCoordinator;
    private readonly IWorkspaceApplyService _workspaceApplyService;
    private readonly IArchitectureWarningService _architectureWarningService;

    public WorkspaceSessionService(
        IWorkspaceStore workspaceStore,
        IWorkspaceShellService workspaceShellService,
        IWorkspaceGenerationCoordinator workspaceGenerationCoordinator,
        IWorkspaceApplyService workspaceApplyService,
        IArchitectureWarningService architectureWarningService)
    {
        _workspaceStore = workspaceStore;
        _workspaceShellService = workspaceShellService;
        _workspaceGenerationCoordinator = workspaceGenerationCoordinator;
        _workspaceApplyService = workspaceApplyService;
        _architectureWarningService = architectureWarningService;
    }

    public async Task OpenProjectAsync(CancellationToken cancellationToken)
    {
        await ExecuteProjectLoadAsync(
            "Scanning project...",
            token => _workspaceShellService.OpenProjectAsync(token),
            cancellationToken);
    }

    public async Task RescanProjectAsync(CancellationToken cancellationToken)
    {
        var currentState = _workspaceStore.State;
        var projectContext = currentState.ProjectContext;
        if (projectContext is null)
        {
            _workspaceStore.SetState(TransitionProjectLoadFailed(
                currentState,
                "Project rescan failed.",
                "Project config is not available."));
            return;
        }

        await ExecuteProjectLoadAsync(
            "Scanning project...",
            token => _workspaceShellService.LoadProjectAsync(projectContext.TargetRootPath, token),
            cancellationToken);
    }

    public void SelectAction(GenerationActionDescriptor? action, IRootGeneratorSessionViewModel? rootSession)
    {
        var currentState = _workspaceStore.State;
        var emptyStateText = action is null
            ? "Build plan to see affected files."
            : $"Build a plan for {action.DisplayName} to see affected files.";
        var statusText = action is null ? "Action cleared." : $"{action.DisplayName} selected.";

        var nextState = ResetPlanState(currentState, emptyStateText, statusText);
        nextState = RecomputeCapabilities(nextState, rootSession);
        _workspaceStore.SetState(nextState);
    }

    public void RefreshRootSessionState(IRootGeneratorSessionViewModel? rootSession)
    {
        _workspaceStore.SetState(RecomputeCapabilities(_workspaceStore.State, rootSession));
    }

    public async Task BuildPlanAsync(IPlanBuildingRootSessionViewModel? rootSession, CancellationToken cancellationToken)
    {
        var currentState = _workspaceStore.State;
        var projectContext = currentState.ProjectContext;
        if (projectContext is null)
        {
            PublishBuildResult(WorkspacePlanBuildResult.Failure(
                "Build plan failed: project is not loaded.",
                "Project config is not available.",
                null,
                PlanPreviewSnapshot.Error("Open a project before building a plan.")),
                rootSession);
            return;
        }

        _workspaceStore.SetState(TransitionBuildStarted(currentState, rootSession));

        var result = await _workspaceGenerationCoordinator.BuildPlanAsync(projectContext, rootSession, cancellationToken);
        PublishBuildResult(result, rootSession);
    }

    public async Task ApplyPlanAsync(CancellationToken cancellationToken)
    {
        var currentState = _workspaceStore.State;
        var preparedPackage = currentState.CurrentPreparedApplyPackage;
        if (preparedPackage is null)
        {
            _workspaceStore.SetState(TransitionApplyBlocked(currentState, "No prepared plan is available."));
            return;
        }

        if (!string.Equals(currentState.CurrentPlanPreviewSnapshot.PackageFingerprint, preparedPackage.Fingerprint, StringComparison.Ordinal))
        {
            _workspaceStore.SetState(TransitionStalePreparedPackage(
                currentState,
                "Apply blocked: the prepared plan is stale.",
                "The prepared plan no longer matches the visible preview."));
            return;
        }

        var applyResult = await _workspaceApplyService.ApplyPlanAsync(preparedPackage, cancellationToken);
        if (!applyResult.Succeeded)
        {
            _workspaceStore.SetState(applyResult.Cancelled
                ? TransitionApplyCancelled(currentState, applyResult.StatusText)
                : TransitionApplyFailed(currentState, applyResult.StatusText, applyResult.ErrorMessage));
            return;
        }

        var reloaded = await _workspaceShellService.LoadProjectAsync(currentState.TargetRootPath!, cancellationToken);
        if (!reloaded.Succeeded || reloaded.Config is null || reloaded.ProjectModel is null)
        {
            _workspaceStore.SetState(TransitionApplySucceededRescanFailed(
                currentState,
                "Plan applied, but project rescan failed.",
                reloaded.ErrorMessage ?? "Project rescan failed."));
            return;
        }

        var warnings = _architectureWarningService.GetWarnings(new ProjectWorkspaceContext(
            reloaded.Config.TargetRootPath,
            reloaded.Config,
            reloaded.ProjectModel));

        _workspaceStore.SetState(TransitionApplySucceededRescanSucceeded(reloaded, warnings));
    }

    private async Task ExecuteProjectLoadAsync(
        string statusText,
        Func<CancellationToken, Task<ProjectLoadResult>> action,
        CancellationToken cancellationToken)
    {
        var currentState = _workspaceStore.State;
        _workspaceStore.SetState(TransitionScanningStarted(currentState, statusText));

        try
        {
            var result = await action(cancellationToken);
            if (!result.Succeeded || result.Config is null || result.ProjectModel is null)
            {
                _workspaceStore.SetState(TransitionProjectLoadFailed(
                    _workspaceStore.State,
                    result.StatusText,
                    result.ErrorMessage ?? "Project load failed."));
                return;
            }

            var projectContext = new ProjectWorkspaceContext(
                result.Config.TargetRootPath,
                result.Config,
                result.ProjectModel);
            var warnings = _architectureWarningService.GetWarnings(projectContext);

            _workspaceStore.SetState(TransitionProjectLoaded(result, warnings));
        }
        catch (Exception ex)
        {
            _workspaceStore.SetState(TransitionProjectLoadFailed(
                _workspaceStore.State,
                "Project load failed.",
                ex.Message,
                ex.ToString()));
        }
    }

    private void PublishBuildResult(WorkspacePlanBuildResult result, IRootGeneratorSessionViewModel? rootSession)
    {
        var currentState = _workspaceStore.State;
        var nextState = currentState with
        {
            CurrentPlan = result.Plan,
            CurrentPreparedApplyPackage = result.PreparedPackage,
            CurrentPlanPreviewSnapshot = result.Snapshot,
            GenerationWarnings = result.PreparedPackage?.Warnings ?? [],
            PlanBuildStatus = result.PlanBuildStatus,
            PlanBuildErrorMessage = result.ErrorMessage,
            LastErrorDetails = result.ErrorDetails,
            StatusText = result.StatusText,
            ApplyResultMessage = null
        };

        if (result.Succeeded && nextState.ProjectContext is not null)
        {
            nextState = nextState with
            {
                Warnings = _architectureWarningService.GetWarnings(nextState.ProjectContext)
            };
        }

        _workspaceStore.SetState(RecomputeCapabilities(nextState, rootSession));
    }

    private static WorkspaceState TransitionScanningStarted(WorkspaceState currentState, string statusText)
    {
        return RecomputeCapabilities(currentState with
        {
            IsScanning = true,
            StatusText = statusText,
            PlanBuildErrorMessage = null,
            LastErrorDetails = null
        }, null);
    }

    private static WorkspaceState TransitionProjectLoaded(
        ProjectLoadResult result,
        IReadOnlyList<CqrsGenerator.Core.Validation.ArchitectureWarning> warnings)
    {
        return RecomputeCapabilities(WorkspaceState.Empty with
        {
            TargetRootPath = result.Config!.TargetRootPath,
            Config = result.Config,
            ProjectModel = result.ProjectModel,
            IsProjectLoaded = true,
            IsScanning = false,
            StatusText = result.StatusText,
            GenerationWarnings = [],
            Warnings = warnings
        }, null);
    }

    private static WorkspaceState TransitionProjectLoadFailed(
        WorkspaceState currentState,
        string statusText,
        string errorMessage,
        string? errorDetails = null)
    {
        return RecomputeCapabilities(currentState with
        {
            CurrentPlan = null,
            CurrentPreparedApplyPackage = null,
            CurrentPlanPreviewSnapshot = PlanPreviewSnapshot.Error(errorMessage),
            GenerationWarnings = [],
            PlanBuildStatus = PlanBuildStatus.Idle,
            PlanBuildErrorMessage = errorMessage,
            LastErrorDetails = errorDetails,
            StatusText = statusText,
            IsProjectLoaded = false,
            IsScanning = false,
            ApplyResultMessage = null,
            Warnings = []
        }, null);
    }

    private static WorkspaceState TransitionBuildStarted(
        WorkspaceState currentState,
        IRootGeneratorSessionViewModel? rootSession)
    {
        return RecomputeCapabilities(currentState with
        {
            PlanBuildStatus = PlanBuildStatus.Building,
            PlanBuildErrorMessage = null,
            LastErrorDetails = null,
            ApplyResultMessage = null,
            StatusText = rootSession is null
                ? "Building plan..."
                : $"Building plan for {rootSession.DisplayName}..."
        }, rootSession);
    }

    private static WorkspaceState TransitionApplyBlocked(WorkspaceState currentState, string statusText)
    {
        return RecomputeCapabilities(currentState with
        {
            ApplyResultMessage = statusText
        }, null);
    }

    private static WorkspaceState TransitionStalePreparedPackage(
        WorkspaceState currentState,
        string statusText,
        string errorMessage)
    {
        return RecomputeCapabilities(currentState with
        {
            CurrentPreparedApplyPackage = null,
            CurrentPlanPreviewSnapshot = PlanPreviewSnapshot.Error(errorMessage),
            GenerationWarnings = [],
            ApplyResultMessage = statusText,
            PlanBuildErrorMessage = errorMessage
        }, null);
    }

    private static WorkspaceState TransitionApplyCancelled(WorkspaceState currentState, string statusText)
    {
        return RecomputeCapabilities(currentState with
        {
            ApplyResultMessage = statusText
        }, null);
    }

    private static WorkspaceState TransitionApplyFailed(
        WorkspaceState currentState,
        string statusText,
        string? errorMessage)
    {
        return RecomputeCapabilities(currentState with
        {
            CurrentPreparedApplyPackage = null,
            GenerationWarnings = [],
            ApplyResultMessage = statusText,
            PlanBuildErrorMessage = errorMessage,
            LastErrorDetails = errorMessage
        }, null);
    }

    private static WorkspaceState TransitionApplySucceededRescanSucceeded(
        ProjectLoadResult result,
        IReadOnlyList<CqrsGenerator.Core.Validation.ArchitectureWarning> warnings)
    {
        return RecomputeCapabilities(WorkspaceState.Empty with
        {
            TargetRootPath = result.Config!.TargetRootPath,
            Config = result.Config,
            ProjectModel = result.ProjectModel,
            IsProjectLoaded = true,
            StatusText = "Plan applied.",
            ApplyResultMessage = "Plan applied and project rescanned.",
            GenerationWarnings = [],
            Warnings = warnings
        }, null);
    }

    private static WorkspaceState TransitionApplySucceededRescanFailed(
        WorkspaceState currentState,
        string statusText,
        string errorMessage)
    {
        return RecomputeCapabilities(currentState with
        {
            CurrentPlan = null,
            CurrentPreparedApplyPackage = null,
            CurrentPlanPreviewSnapshot = PlanPreviewSnapshot.Error(errorMessage),
            GenerationWarnings = [],
            PlanBuildStatus = PlanBuildStatus.Idle,
            PlanBuildErrorMessage = errorMessage,
            LastErrorDetails = errorMessage,
            StatusText = statusText,
            ApplyResultMessage = statusText,
            IsProjectLoaded = false,
            IsScanning = false
        }, null);
    }

    private static WorkspaceState RecomputeCapabilities(WorkspaceState state, IRootGeneratorSessionViewModel? rootSession)
    {
        var canBuild = !state.IsScanning && state.ProjectContext is not null && rootSession?.CanBuildPlan == true;
        var canApply =
            !state.IsScanning
            && state.CurrentPreparedApplyPackage is not null
            && !state.CurrentPreparedApplyPackage.HasConflicts
            && string.Equals(state.CurrentPreparedApplyPackage.Fingerprint, state.CurrentPlanPreviewSnapshot.PackageFingerprint, StringComparison.Ordinal);

        return state with
        {
            CanBuildPlan = canBuild,
            CanApplyPlan = canApply
        };
    }

    private static WorkspaceState ResetPlanState(WorkspaceState state, string emptyStateText, string statusText)
    {
        return state with
        {
            CurrentPlan = null,
            CurrentPreparedApplyPackage = null,
            CurrentPlanPreviewSnapshot = PlanPreviewSnapshot.Empty(emptyStateText),
            GenerationWarnings = [],
            PlanBuildStatus = PlanBuildStatus.Idle,
            PlanBuildErrorMessage = null,
            LastErrorDetails = null,
            ApplyResultMessage = null,
            StatusText = statusText,
            CanApplyPlan = false
        };
    }
}
