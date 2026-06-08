namespace CqrsGenerator.Gui.Services;

public interface IProjectScanService
{
    Task<ProjectScanResult> ScanAsync(string targetRootPath, CancellationToken cancellationToken);
}
