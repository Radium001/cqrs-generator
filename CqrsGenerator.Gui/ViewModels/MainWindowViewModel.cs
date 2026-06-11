using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Services;
using System.ComponentModel;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class MainWindowViewModel
{
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IWorkspaceSessionService _workspaceSessionService;
    private IRootGeneratorSessionViewModel? _activeRootSession;
    private INotifyPropertyChanged? _activeRootSessionNotifier;

    public MainWindowViewModel(
        IThemeService themeService,
        IWorkspaceStore workspaceStore,
        IWorkspaceSessionService workspaceSessionService,
        GeneratorHostViewModel generatorHostViewModel,
        AppUpdateViewModel updates)
    {
        _workspaceStore = workspaceStore;
        _workspaceSessionService = workspaceSessionService;

        Updates = updates;
        MainMenu = new MainMenuViewModel(themeService, Updates);
        GeneratorHost = generatorHostViewModel;
        GenerationPlanPreview = new GenerationPlanPreviewViewModel(_workspaceStore);
        MessagesPanel = new MessagesPanelViewModel(_workspaceStore);

        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync, CanOpenProject);
        RescanProjectCommand = new AsyncRelayCommand(RescanProjectAsync, CanRescanProject);
        BuildPlanCommand = new AsyncRelayCommand(BuildPlanAsync, CanBuildPlan);
        ApplyPlanCommand = new AsyncRelayCommand(ApplyPlanAsync, CanApplyPlan);

        MainMenu.OpenProjectCommand = OpenProjectCommand;
        MainMenu.RescanProjectCommand = RescanProjectCommand;
        MessagesPanel.BuildPlanCommand = BuildPlanCommand;
        MessagesPanel.ApplyCommand = ApplyPlanCommand;
        GeneratorHost.SelectedRootActionChanged += OnSelectedRootActionChanged;
        GeneratorHost.RootSessionChanged += OnRootSessionChanged;
        GeneratorHost.PropertyChanged += OnGeneratorHostPropertyChanged;
        _workspaceStore.StateChanged += (_, state) => SyncWorkspaceChrome(state);
        SyncWorkspaceChrome(_workspaceStore.State);
    }

    public string Title => "CQRS Generator GUI";

    public MainMenuViewModel MainMenu { get; }

    public AppUpdateViewModel Updates { get; }

    public GeneratorHostViewModel GeneratorHost { get; }

    public GenerationPlanPreviewViewModel GenerationPlanPreview { get; }

    public MessagesPanelViewModel MessagesPanel { get; }

    public IAsyncRelayCommand OpenProjectCommand { get; }

    public IAsyncRelayCommand RescanProjectCommand { get; }

    public IAsyncRelayCommand BuildPlanCommand { get; }

    public IAsyncRelayCommand ApplyPlanCommand { get; }

    private bool CanOpenProject() => !_workspaceStore.State.IsScanning && !_workspaceStore.State.IsApplyingPlan;

    private bool CanRescanProject() => !_workspaceStore.State.IsScanning && !_workspaceStore.State.IsApplyingPlan && _workspaceStore.State.ProjectContext is not null;

    private async Task OpenProjectAsync()
    {
        await _workspaceSessionService.OpenProjectAsync(CancellationToken.None);
        RefreshPlanActions();
    }

    private async Task RescanProjectAsync()
    {
        await _workspaceSessionService.RescanProjectAsync(CancellationToken.None);
        RefreshPlanActions();
    }

    private void OnSelectedRootActionChanged(GenerationActionDescriptor? action)
    {
        if (action is null)
        {
            _workspaceSessionService.SelectAction(null, null);
        }
        RefreshPlanActions();
    }

    private void OnRootSessionChanged(CqrsGenerator.Gui.ViewModels.Generators.IRootGeneratorSessionViewModel? session)
    {
        if (_activeRootSessionNotifier is not null)
        {
            _activeRootSessionNotifier.PropertyChanged -= OnRootSessionPropertyChanged;
        }

        _activeRootSession = session;
        _activeRootSessionNotifier = session as INotifyPropertyChanged;
        if (_activeRootSessionNotifier is not null)
        {
            _activeRootSessionNotifier.PropertyChanged += OnRootSessionPropertyChanged;
        }

        _workspaceSessionService.RefreshRootSessionState(_activeRootSession);
        RefreshPlanActions();
    }

    private void OnGeneratorHostPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(GeneratorHostViewModel.ActiveBreadcrumbText))
        {
            MainMenu.SelectedActionText = GeneratorHost.ActiveBreadcrumbText;
        }
    }

    private async Task BuildPlanAsync()
    {
        await _workspaceSessionService.BuildPlanAsync(_activeRootSession, CancellationToken.None);
        RefreshPlanActions();
    }

    private async Task ApplyPlanAsync()
    {
        await _workspaceSessionService.ApplyPlanAsync(CancellationToken.None);
        RefreshPlanActions();
    }

    private bool CanBuildPlan() => _workspaceStore.State.CanBuildPlan;

    private bool CanApplyPlan() => _workspaceStore.State.CanApplyPlan;

    private void OnRootSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IRootGeneratorSessionViewModel.CanBuildPlan))
        {
            _workspaceSessionService.RefreshRootSessionState(_activeRootSession);
            RefreshPlanActions();
        }
    }

    private void RefreshPlanActions()
    {
        BuildPlanCommand.NotifyCanExecuteChanged();
        ApplyPlanCommand.NotifyCanExecuteChanged();
        OpenProjectCommand.NotifyCanExecuteChanged();
        RescanProjectCommand.NotifyCanExecuteChanged();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Updates.InitializeAsync(cancellationToken);

    private void SyncWorkspaceChrome(WorkspaceState state)
    {
        MainMenu.TargetRootPath = state.TargetRootPath ?? "Target project is not selected";
        MainMenu.IsProjectLoaded = state.IsProjectLoaded;
        MainMenu.IsScanning = state.IsScanning;
        MainMenu.IsApplyingPlan = state.IsApplyingPlan;
        MainMenu.StatusText = state.StatusText;

        RefreshPlanActions();
    }
}
