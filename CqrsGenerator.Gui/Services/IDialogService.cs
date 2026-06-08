using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Services;

public interface IDialogService
{
    Task<bool> ConfirmPlanApplyAsync(PreparedApplyPackage package, CancellationToken cancellationToken);
}
