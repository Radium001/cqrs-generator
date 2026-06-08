using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Gui.Services;

public sealed class ProjectScanService : IProjectScanService
{
    public Task<ProjectScanResult> ScanAsync(string targetRootPath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.GetFullPath(targetRootPath);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"Target root does not exist: {fullPath}");
            }

            var config = GeneratorConfig.ForTargetRoot(fullPath);
            var project = new ProjectDiscovery(config).Discover();

            cancellationToken.ThrowIfCancellationRequested();
            return new ProjectScanResult(config, project);
        }, cancellationToken);
    }
}
