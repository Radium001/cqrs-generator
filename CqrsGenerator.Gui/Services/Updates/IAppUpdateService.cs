namespace CqrsGenerator.Gui.Services.Updates;

public interface IAppUpdateService
{
    bool IsSupported { get; }

    string CurrentVersion { get; }

    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken);

    Task DownloadUpdatesAsync(UpdateCheckResult update, IProgress<int> progress, CancellationToken cancellationToken);

    void ApplyUpdatesAndRestart(UpdateCheckResult update);
}
