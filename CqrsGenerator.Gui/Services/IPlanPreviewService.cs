using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IPlanPreviewService
{
    PlanPreviewSnapshot Build(PreparedApplyPackage? package);
}
