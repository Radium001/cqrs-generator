using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.ViewModels.Generators;
using System.ComponentModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class GeneratorStackViewModel : ObservableObject, IEmbeddedSessionHost
{
    private readonly ObservableCollection<SessionEntry> _sessions;
    private readonly IWorkspaceStore _workspaceStore;
    private bool _isSynchronizingWorkspace;
    private ProjectWorkspaceContext? _lastProjectContext;

    public GeneratorStackViewModel(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
        _sessions = [];
        Breadcrumbs = [];
        _lastProjectContext = workspaceStore.State.ProjectContext;
        _workspaceStore.StateChanged += OnWorkspaceStateChanged;

        CloseRootCommand = new RelayCommand(CloseRoot, () => HasRootSession);
        CancelEmbeddedCommand = new RelayCommand(CancelEmbedded, () => CanCancelEmbedded);
        CompleteEmbeddedCommand = new RelayCommand(CompleteEmbedded, () => CanCompleteEmbedded);
    }

    public ObservableCollection<GeneratorBreadcrumbItem> Breadcrumbs { get; }

    public IRelayCommand CloseRootCommand { get; }

    public IRelayCommand CancelEmbeddedCommand { get; }

    public IRelayCommand CompleteEmbeddedCommand { get; }

    public IGeneratorSessionViewModel? ActiveSession => _sessions.LastOrDefault()?.Session;

    public IRootGeneratorSessionViewModel? RootSession => _sessions
        .Select(entry => entry.Session)
        .OfType<IRootGeneratorSessionViewModel>()
        .FirstOrDefault();

    public bool HasActiveSession => ActiveSession is not null;

    public bool HasRootSession => RootSession is not null;

    public bool HasEmbeddedSession => ActiveSession is not null && !ActiveSession.IsRoot;

    public bool CanCancelEmbedded => HasEmbeddedSession;

    public bool CanCompleteEmbedded => ActiveSession is IEmbeddedGeneratorSessionViewModel { CanComplete: true };

    public string ActiveSessionTitle => ActiveSession?.DisplayName ?? "Scenario Editor";

    public string ActiveSessionSummary => ActiveSession?.Summary ?? "Choose an action to start building a scenario.";

    public string BreadcrumbText => Breadcrumbs.Count == 0
        ? "No action selected"
        : string.Join(" > ", Breadcrumbs.Select(item => item.Title));

    public event Action<IRootGeneratorSessionViewModel?>? RootSessionChanged;

    public void OpenRoot(IRootGeneratorSessionViewModel rootSession)
    {
        var previousRootSession = RootSession;
        UnsubscribeAllSessions();
        _sessions.Clear();
        _sessions.Add(new RootSessionEntry(rootSession));
        SubscribeSession(rootSession);
        var projectContext = _workspaceStore.State.ProjectContext;
        UpdateWorkspace(rootSession, projectContext);
        _lastProjectContext = projectContext;
        RefreshState();
        NotifyRootSessionChanged(previousRootSession, RootSession);
    }

    public void Open<TDraft>(IEmbeddedGeneratorSessionViewModel<TDraft> childSession, Action<TDraft> onCompleted)
    {
        _sessions.Add(new SessionEntry<TDraft>(childSession, onCompleted));
        SubscribeSession(childSession);
        var projectContext = _workspaceStore.State.ProjectContext;
        UpdateWorkspace(childSession, projectContext);
        _lastProjectContext = projectContext;
        RefreshState();
    }

    private void CancelEmbedded()
    {
        if (!HasEmbeddedSession)
        {
            return;
        }

        UnsubscribeSession(_sessions[^1].Session);
        _sessions.RemoveAt(_sessions.Count - 1);
        RefreshState();
    }

    private void CompleteEmbedded()
    {
        if (!HasEmbeddedSession)
        {
            return;
        }

        var activeEntry = _sessions[^1];
        if (!activeEntry.CanComplete)
        {
            return;
        }

        activeEntry.Complete();
        UnsubscribeSession(activeEntry.Session);
        _sessions.RemoveAt(_sessions.Count - 1);
        RefreshState();
    }

    private void CloseRoot()
    {
        var previousRootSession = RootSession;
        UnsubscribeAllSessions();
        _sessions.Clear();
        RefreshState();
        NotifyRootSessionChanged(previousRootSession, RootSession);
    }

    private void RefreshState()
    {
        Breadcrumbs.Clear();
        for (var index = 0; index < _sessions.Count; index++)
        {
            Breadcrumbs.Add(new GeneratorBreadcrumbItem(
                _sessions[index].Session.DisplayName,
                index == _sessions.Count - 1));
        }

        OnPropertyChanged(nameof(ActiveSession));
        OnPropertyChanged(nameof(RootSession));
        OnPropertyChanged(nameof(HasActiveSession));
        OnPropertyChanged(nameof(HasRootSession));
        OnPropertyChanged(nameof(HasEmbeddedSession));
        OnPropertyChanged(nameof(CanCancelEmbedded));
        OnPropertyChanged(nameof(CanCompleteEmbedded));
        OnPropertyChanged(nameof(ActiveSessionTitle));
        OnPropertyChanged(nameof(ActiveSessionSummary));
        OnPropertyChanged(nameof(BreadcrumbText));

        CloseRootCommand.NotifyCanExecuteChanged();
        CancelEmbeddedCommand.NotifyCanExecuteChanged();
        CompleteEmbeddedCommand.NotifyCanExecuteChanged();
    }

    private void OnWorkspaceStateChanged(object? sender, WorkspaceState state)
    {
        if (_isSynchronizingWorkspace || EqualityComparer<ProjectWorkspaceContext?>.Default.Equals(_lastProjectContext, state.ProjectContext))
        {
            return;
        }

        _isSynchronizingWorkspace = true;

        try
        {
            _lastProjectContext = state.ProjectContext;

            foreach (var session in _sessions.Select(entry => entry.Session))
            {
                UpdateWorkspace(session, state.ProjectContext);
            }

            RefreshState();
        }
        finally
        {
            _isSynchronizingWorkspace = false;
        }
    }

    private void OnTrackedSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IGeneratorSessionViewModel.DisplayName)
            or nameof(IGeneratorSessionViewModel.Summary)
            or nameof(IEmbeddedGeneratorSessionViewModel.CanComplete)
            or nameof(IRootGeneratorSessionViewModel.CanBuildPlan))
        {
            RefreshState();
        }
    }

    private void SubscribeSession(IGeneratorSessionViewModel session)
    {
        if (session is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += OnTrackedSessionPropertyChanged;
        }
    }

    private void UnsubscribeSession(IGeneratorSessionViewModel session)
    {
        if (session is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= OnTrackedSessionPropertyChanged;
        }
    }

    private void UnsubscribeAllSessions()
    {
        foreach (var entry in _sessions)
        {
            UnsubscribeSession(entry.Session);
        }
    }

    private static void UpdateWorkspace(IGeneratorSessionViewModel session, ProjectWorkspaceContext? workspaceContext)
    {
        if (session is IWorkspaceAwareGeneratorSessionViewModel workspaceAwareSession)
        {
            workspaceAwareSession.UpdateWorkspace(workspaceContext);
        }
    }

    private void NotifyRootSessionChanged(IRootGeneratorSessionViewModel? previousRootSession, IRootGeneratorSessionViewModel? currentRootSession)
    {
        if (!ReferenceEquals(previousRootSession, currentRootSession))
        {
            RootSessionChanged?.Invoke(currentRootSession);
        }
    }

    private abstract record SessionEntry(IGeneratorSessionViewModel Session)
    {
        public abstract bool CanComplete { get; }

        public abstract void Complete();
    }

    private sealed record SessionEntry<TDraft>(
        IEmbeddedGeneratorSessionViewModel<TDraft> EmbeddedSession,
        Action<TDraft> OnCompleted) : SessionEntry(EmbeddedSession)
    {
        public override bool CanComplete => EmbeddedSession.CanComplete;

        public override void Complete()
        {
            OnCompleted(EmbeddedSession.BuildDraft());
        }
    }

    private sealed record RootSessionEntry(IRootGeneratorSessionViewModel RootSession) : SessionEntry(RootSession)
    {
        public override bool CanComplete => false;

        public override void Complete()
        {
        }
    }
}
