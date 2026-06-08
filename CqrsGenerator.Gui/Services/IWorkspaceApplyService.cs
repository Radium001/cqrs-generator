using CqrsGenerator.Gui.ViewModels;

using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Services;

public interface IWorkspaceApplyService
{
    Task<WorkspaceApplyExecutionResult> ApplyPlanAsync(PreparedApplyPackage package, CancellationToken cancellationToken);
}
