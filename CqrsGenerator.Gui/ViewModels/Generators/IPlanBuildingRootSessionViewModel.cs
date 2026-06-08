using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public interface IPlanBuildingRootSessionViewModel : IRootGeneratorSessionViewModel
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext);
}
