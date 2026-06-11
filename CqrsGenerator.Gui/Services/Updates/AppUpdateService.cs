using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace CqrsGenerator.Gui.Services.Updates;

public sealed class AppUpdateService : IAppUpdateService
{
    private readonly AppUpdateOptions _options;
    private readonly Lazy<UpdateManager?> _updateManager;

    public AppUpdateService(AppUpdateOptions options)
    {
        _options = options;
        _updateManager = new Lazy<UpdateManager?>(CreateUpdateManager);
    }

    public bool IsSupported => _updateManager.Value?.IsInstalled == true;

    public string CurrentVersion
    {
        get
        {
            var manager = _updateManager.Value;
            if (manager?.CurrentVersion is not null)
            {
                return manager.CurrentVersion.ToString();
            }

            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                ?? "dev";

            var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
            return plusIndex > 0 ? version[..plusIndex] : version;
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manager = _updateManager.Value;
        if (manager is null)
        {
            return new UpdateCheckResult(
                false,
                false,
                CurrentVersion,
                null,
                "Updates are not configured for this build.",
                null);
        }

        if (!manager.IsInstalled)
        {
            return new UpdateCheckResult(
                false,
                false,
                CurrentVersion,
                null,
                "Updates are available only in the installed app.",
                null);
        }

        var pending = manager.UpdatePendingRestart;
        if (pending is not null)
        {
            return new UpdateCheckResult(
                true,
                true,
                CurrentVersion,
                pending.Version.ToString(),
                "Update is ready to install.",
                pending);
        }

        var updateInfo = await manager.CheckForUpdatesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (updateInfo is null)
        {
            return new UpdateCheckResult(
                true,
                false,
                CurrentVersion,
                null,
                "You are using the latest version.",
                null);
        }

        return new UpdateCheckResult(
            true,
            true,
            CurrentVersion,
            updateInfo.TargetFullRelease.Version.ToString(),
            "A new version is available.",
            updateInfo);
    }

    public async Task DownloadUpdatesAsync(UpdateCheckResult update, IProgress<int> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();

        var manager = _updateManager.Value ?? throw new InvalidOperationException("Updates are not configured for this build.");

        if (update.NativeUpdateInfo is UpdateInfo updateInfo)
        {
            await manager.DownloadUpdatesAsync(updateInfo, value => progress.Report(value), cancellationToken);
            return;
        }

        if (update.NativeUpdateInfo is VelopackAsset)
        {
            progress.Report(100);
            return;
        }

        throw new InvalidOperationException("No update is available to download.");
    }

    public void ApplyUpdatesAndRestart(UpdateCheckResult update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var manager = _updateManager.Value ?? throw new InvalidOperationException("Updates are not configured for this build.");
        var asset = update.NativeUpdateInfo switch
        {
            UpdateInfo updateInfo => updateInfo.TargetFullRelease,
            VelopackAsset pendingAsset => pendingAsset,
            _ => manager.UpdatePendingRestart
        };

        manager.ApplyUpdatesAndRestart(asset);
    }

    private UpdateManager? CreateUpdateManager()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.UpdateUrl))
            {
                return null;
            }

            if (Uri.TryCreate(_options.UpdateUrl, UriKind.Absolute, out var uri)
                && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                return new UpdateManager(new GithubSource(_options.UpdateUrl, accessToken: null, prerelease: _options.IncludePrereleases));
            }

            return new UpdateManager(_options.UpdateUrl);
        }
        catch
        {
            return null;
        }
    }
}
