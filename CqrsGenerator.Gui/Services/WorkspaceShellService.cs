namespace CqrsGenerator.Gui.Services;

public sealed class WorkspaceShellService : IWorkspaceShellService
{
    private readonly IProjectOpenService _projectOpenService;
    private readonly IProjectScanService _projectScanService;

    public WorkspaceShellService(
        IProjectOpenService projectOpenService,
        IProjectScanService projectScanService)
    {
        _projectOpenService = projectOpenService;
        _projectScanService = projectScanService;
    }

    public async Task<ProjectLoadResult> OpenProjectAsync(CancellationToken cancellationToken)
    {
        var targetRootPath = await _projectOpenService.OpenProjectAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(targetRootPath))
        {
            return ProjectLoadResult.Failure("Project open cancelled.", "Target project was not selected.");
        }

        return await LoadProjectAsync(targetRootPath, cancellationToken);
    }

    public async Task<ProjectLoadResult> LoadProjectAsync(string targetRootPath, CancellationToken cancellationToken)
    {
        var scanResult = await _projectScanService.ScanAsync(targetRootPath, cancellationToken);
        return ProjectLoadResult.Success("Project discovery completed.", scanResult.Config, scanResult.Project);
    }
}
