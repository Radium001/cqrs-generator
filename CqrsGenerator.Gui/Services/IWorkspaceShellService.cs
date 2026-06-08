using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IWorkspaceShellService
{
    Task<ProjectLoadResult> OpenProjectAsync(CancellationToken cancellationToken);

    Task<ProjectLoadResult> LoadProjectAsync(string targetRootPath, CancellationToken cancellationToken);
}
