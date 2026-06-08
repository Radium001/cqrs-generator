using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public interface IWorkspaceAwareGeneratorSessionViewModel
{
    void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext);
}
