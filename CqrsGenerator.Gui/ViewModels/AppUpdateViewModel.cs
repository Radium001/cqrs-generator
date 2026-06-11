using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Services.Updates;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class AppUpdateViewModel : ObservableObject
{
    private readonly IAppUpdateService _updateService;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IDialogService _dialogService;
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private UpdateCheckResult? _latestResult;

    public AppUpdateViewModel(
        IAppUpdateService updateService,
        IWorkspaceStore workspaceStore,
        IDialogService dialogService)
    {
        _updateService = updateService;
        _workspaceStore = workspaceStore;
        _dialogService = dialogService;

        CurrentVersion = _updateService.CurrentVersion;
        StatusKind = _updateService.IsSupported ? UpdateStatusKind.Unknown : UpdateStatusKind.UnsupportedEnvironment;
        StatusText = _updateService.IsSupported
            ? "Updates were not checked yet."
            : "Updates are available only in the installed app.";
        BadgeText = _updateService.IsSupported ? "Updates not checked" : "Dev build";

        CheckForUpdatesCommand = new AsyncRelayCommand(() => CheckForUpdatesAsync(), CanCheckForUpdates);
        DownloadAndInstallUpdateCommand = new AsyncRelayCommand(DownloadAndInstallUpdateAsync, CanDownloadAndInstallUpdate);

        _workspaceStore.StateChanged += (_, _) => RefreshCommands();
    }

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private string? _availableVersion;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _badgeText = string.Empty;

    [ObservableProperty]
    private UpdateStatusKind _statusKind;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private int _downloadProgress;

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    public IAsyncRelayCommand DownloadAndInstallUpdateCommand { get; }

    public bool IsLatest => StatusKind == UpdateStatusKind.Latest;

    public bool IsUpdateAvailable => StatusKind is UpdateStatusKind.UpdateAvailable or UpdateStatusKind.ReadyToRestart;

    public bool IsDownloadingStatus => StatusKind == UpdateStatusKind.Downloading;

    public bool IsStatusMuted => !IsLatest && !IsUpdateAvailable && !IsDownloadingStatus;

    public string VersionSummary => string.IsNullOrWhiteSpace(AvailableVersion)
        ? $"Current version: {CurrentVersion}"
        : $"Current version: {CurrentVersion}. Available: {AvailableVersion}";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_updateService.IsSupported)
        {
            SetUnsupported();
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            await CheckForUpdatesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool CanCheckForUpdates() =>
        _updateService.IsSupported && !IsChecking && !IsDownloading;

    private bool CanDownloadAndInstallUpdate() =>
        _updateService.IsSupported
        && _latestResult?.IsUpdateAvailable == true
        && !IsChecking
        && !IsDownloading
        && CanRestartForUpdate();

    private async Task CheckForUpdatesAsync()
    {
        await CheckForUpdatesAsync(CancellationToken.None);
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        if (!await _updateGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            IsChecking = true;
            StatusKind = UpdateStatusKind.Checking;
            StatusText = "Checking for updates...";
            BadgeText = "Checking updates...";
            RefreshDerivedProperties();
            RefreshCommands();

            var result = await _updateService.CheckForUpdatesAsync(cancellationToken);
            _latestResult = result;
            CurrentVersion = result.CurrentVersion;
            AvailableVersion = result.AvailableVersion;

            if (!result.IsSupported)
            {
                SetUnsupported(result.Message);
            }
            else if (result.IsUpdateAvailable)
            {
                StatusKind = result.NativeUpdateInfo is Velopack.VelopackAsset
                    ? UpdateStatusKind.ReadyToRestart
                    : UpdateStatusKind.UpdateAvailable;
                StatusText = result.Message ?? "A new version is available.";
                BadgeText = string.IsNullOrWhiteSpace(result.AvailableVersion)
                    ? "Update available"
                    : $"Update {result.AvailableVersion}";
            }
            else
            {
                StatusKind = UpdateStatusKind.Latest;
                StatusText = result.Message ?? "You are using the latest version.";
                BadgeText = "Latest version";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusKind = UpdateStatusKind.Failed;
            StatusText = $"Update check failed: {ex.Message}";
            BadgeText = "Update check failed";
        }
        finally
        {
            IsChecking = false;
            _updateGate.Release();
            RefreshDerivedProperties();
            RefreshCommands();
        }
    }

    private async Task DownloadAndInstallUpdateAsync()
    {
        if (_latestResult is null || !_latestResult.IsUpdateAvailable)
        {
            return;
        }

        if (!CanRestartForUpdate(out var blockingReason))
        {
            StatusKind = UpdateStatusKind.Failed;
            StatusText = blockingReason ?? "Finish the current operation before updating.";
            BadgeText = "Update blocked";
            RefreshDerivedProperties();
            RefreshCommands();
            return;
        }

        var confirmed = await _dialogService.ConfirmUpdateInstallAsync(
            _latestResult.AvailableVersion,
            "The application will restart to finish installing the update.",
            CancellationToken.None);
        if (!confirmed)
        {
            return;
        }

        if (!await _updateGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            IsDownloading = true;
            DownloadProgress = 0;
            StatusKind = UpdateStatusKind.Downloading;
            StatusText = "Downloading update...";
            BadgeText = "Downloading 0%";
            RefreshDerivedProperties();
            RefreshCommands();

            var progress = new Progress<int>(value =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DownloadProgress = Math.Clamp(value, 0, 100);
                    BadgeText = $"Downloading {DownloadProgress}%";
                    StatusText = $"Downloading update {DownloadProgress}%...";
                });
            });

            await _updateService.DownloadUpdatesAsync(_latestResult, progress, CancellationToken.None);

            StatusKind = UpdateStatusKind.ReadyToRestart;
            DownloadProgress = 100;
            StatusText = "Update downloaded. Restarting...";
            BadgeText = "Restarting...";
            RefreshDerivedProperties();
            RefreshCommands();

            _updateService.ApplyUpdatesAndRestart(_latestResult);
        }
        catch (Exception ex)
        {
            StatusKind = UpdateStatusKind.Failed;
            StatusText = $"Update failed: {ex.Message}";
            BadgeText = "Update failed";
        }
        finally
        {
            IsDownloading = false;
            _updateGate.Release();
            RefreshDerivedProperties();
            RefreshCommands();
        }
    }

    private bool CanRestartForUpdate() => CanRestartForUpdate(out _);

    private bool CanRestartForUpdate(out string? blockingReason)
    {
        var state = _workspaceStore.State;
        if (state.IsScanning)
        {
            blockingReason = "Wait until the project scan finishes before updating.";
            return false;
        }

        if (state.PlanBuildStatus == PlanBuildStatus.Building)
        {
            blockingReason = "Wait until the plan build finishes before updating.";
            return false;
        }

        if (state.IsApplyingPlan)
        {
            blockingReason = "Wait until the current generation finishes before updating.";
            return false;
        }

        blockingReason = null;
        return true;
    }

    private void SetUnsupported(string? message = null)
    {
        _latestResult = null;
        AvailableVersion = null;
        StatusKind = UpdateStatusKind.UnsupportedEnvironment;
        StatusText = message ?? "Updates are available only in the installed app.";
        BadgeText = "Dev build";
        RefreshDerivedProperties();
        RefreshCommands();
    }

    partial void OnStatusKindChanged(UpdateStatusKind value) => RefreshDerivedProperties();

    partial void OnCurrentVersionChanged(string value) => OnPropertyChanged(nameof(VersionSummary));

    partial void OnAvailableVersionChanged(string? value) => OnPropertyChanged(nameof(VersionSummary));

    partial void OnIsCheckingChanged(bool value) => RefreshCommands();

    partial void OnIsDownloadingChanged(bool value) => RefreshCommands();

    private void RefreshDerivedProperties()
    {
        OnPropertyChanged(nameof(IsLatest));
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(IsDownloadingStatus));
        OnPropertyChanged(nameof(IsStatusMuted));
        OnPropertyChanged(nameof(VersionSummary));
    }

    private void RefreshCommands()
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        DownloadAndInstallUpdateCommand.NotifyCanExecuteChanged();
    }
}
