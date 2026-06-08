using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Services;

public sealed class WorkspaceApplyService : IWorkspaceApplyService
{
    private readonly IDialogService _dialogService;
    private readonly StrictPlanApplier _strictPlanApplier;

    public WorkspaceApplyService(IDialogService dialogService, StrictPlanApplier strictPlanApplier)
    {
        _dialogService = dialogService;
        _strictPlanApplier = strictPlanApplier;
    }

    public async Task<WorkspaceApplyExecutionResult> ApplyPlanAsync(PreparedApplyPackage package, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (package.HasConflicts)
        {
            return WorkspaceApplyExecutionResult.Failure("Apply blocked: the plan has conflicts.", "Resolve plan conflicts before applying.");
        }

        if (!await _dialogService.ConfirmPlanApplyAsync(package, cancellationToken))
        {
            return WorkspaceApplyExecutionResult.CancelledByUser("Plan apply cancelled.");
        }

        try
        {
            _strictPlanApplier.Apply(package);
            return WorkspaceApplyExecutionResult.Success("Plan applied.");
        }
        catch (Exception ex)
        {
            return WorkspaceApplyExecutionResult.Failure("Plan apply failed.", ex.Message);
        }
    }
}
