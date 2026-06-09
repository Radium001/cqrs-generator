using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.ViewModels.Generators;
using System.ComponentModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class GeneratorStackViewModel : ObservableObject, IEmbeddedSessionHost
{
    private readonly IWorkspaceStore _workspaceStore;
    private readonly GenerationSession _generationSession;
    private readonly IGenerationSessionNavigator _navigator;
    private readonly GeneratorDefinitionCatalog _definitionCatalog;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Guid, IGeneratorSessionViewModel> _nodeEditors;
    private IRootGeneratorSessionViewModel? _rootSession;
    private IGeneratorSessionViewModel? _activeSession;
    private bool _isSynchronizingWorkspace;
    private ProjectWorkspaceContext? _lastProjectContext;

    public GeneratorStackViewModel(
        IWorkspaceStore workspaceStore,
        GenerationSession generationSession,
        IGenerationSessionNavigator navigator,
        GeneratorDefinitionCatalog definitionCatalog,
        IServiceProvider serviceProvider)
    {
        _workspaceStore = workspaceStore;
        _generationSession = generationSession;
        _navigator = navigator;
        _definitionCatalog = definitionCatalog;
        _serviceProvider = serviceProvider;
        _nodeEditors = [];
        Breadcrumbs = [];
        SessionBreadcrumbs = [];
        _lastProjectContext = workspaceStore.State.ProjectContext;
        _workspaceStore.StateChanged += OnWorkspaceStateChanged;

        _generationSession.PropertyChanged += OnSessionPropertyChanged;

        CloseRootCommand = new RelayCommand(CloseRoot, () => _rootSession is not null);
        NavigateToParentCommand = new RelayCommand(NavigateToParent, () => CanNavigateToParent);
        CompleteChildNodeCommand = new RelayCommand(CompleteChildNode, () => CanCompleteChildNode);
    }

    public ObservableCollection<GeneratorBreadcrumbItem> Breadcrumbs { get; }

    public ObservableCollection<GeneratorBreadcrumbItem> SessionBreadcrumbs { get; }

    public IRelayCommand CloseRootCommand { get; }

    public IRelayCommand NavigateToParentCommand { get; }

    public IRelayCommand CompleteChildNodeCommand { get; }

    public IGeneratorSessionViewModel? ActiveSession => _activeSession;

    public IRootGeneratorSessionViewModel? RootSession => _rootSession;

    public GeneratorNode? ActiveNode => _generationSession.ActiveNode;

    public bool HasActiveNode => _generationSession.ActiveNode is not null;

    public bool HasActiveSession => _activeSession is not null;

    public bool HasRootSession => _rootSession is not null;

    public bool CanNavigateToParent => _generationSession.ActiveNode?.ParentId is not null;

    public bool HasChildNode => _generationSession.ActiveNode?.ParentId is not null;

    public bool CanCompleteChildNode => _generationSession.ActiveNode?.ParentId is not null;

    public bool HasParentNode => _generationSession.ActiveNode?.ParentId is not null;

    public string ActiveSessionTitle => _activeSession?.DisplayName ?? "Scenario Editor";

    public string ActiveSessionSummary => _activeSession?.Summary ?? "Choose an action to start building a scenario.";

    public string BreadcrumbText => Breadcrumbs.Count == 0
        ? "No action selected"
        : string.Join(" > ", Breadcrumbs.Select(item => item.Title));

    public event Action<IRootGeneratorSessionViewModel?>? RootSessionChanged;

    public void OpenRoot(IRootGeneratorSessionViewModel rootSession)
    {
        var previousRootSession = _rootSession;
        UnsubscribeAllEditors();
        _nodeEditors.Clear();
        _rootSession = rootSession;
        SubscribeSession(rootSession);

        if (rootSession is IGeneratorNodeEditorViewModel { Node: null } editor)
        {
            var kind = GetRootKind(rootSession);
            if (_definitionCatalog.HasDefinition(kind))
            {
                var state = _definitionCatalog.GetDefinition(kind).CreateInitialState(new GeneratorCreationContext());
                var node = _navigator.CreateRoot(kind, state);
                editor.Node = node;
            }
        }

        switch (rootSession)
        {
            case AddQueryRootSessionViewModel q: q.SetGenerationSession(_generationSession, _navigator); break;
            case CommandRootSessionViewModel c: c.SetGenerationSession(_generationSession, _navigator); break;
            case RepositoryRootSessionViewModel r: r.SetGenerationSession(_generationSession, _navigator); break;
            case AddWebPageRootSessionViewModel w: w.SetGenerationSession(_generationSession, _navigator); break;
        }

        var projectContext = _workspaceStore.State.ProjectContext;
        UpdateWorkspace(rootSession, projectContext);
        _lastProjectContext = projectContext;
        _activeSession = rootSession;
        RefreshState();
        NotifyRootSessionChanged(previousRootSession, _rootSession);
    }

    private static GeneratorNodeKind GetRootKind(IRootGeneratorSessionViewModel session) => session switch
    {
        AddQueryRootSessionViewModel => GeneratorNodeKind.Query,
        CommandRootSessionViewModel => GeneratorNodeKind.Command,
        RepositoryRootSessionViewModel => GeneratorNodeKind.Repository,
        AddWebPageRootSessionViewModel => GeneratorNodeKind.WebPage,
        CreateFeatureRootSessionViewModel => GeneratorNodeKind.Feature,
        DtoRootSessionViewModel => GeneratorNodeKind.Dto,
        EntityRootSessionViewModel => GeneratorNodeKind.Entity,
        _ => throw new InvalidOperationException($"Unknown session type: {session.GetType()}")
    };

    public GeneratorNode OpenRootNode(GeneratorNodeKind kind, object state)
    {
        var node = _navigator.CreateRoot(kind, state);
        _navigator.OpenNode(node.Id);
        RefreshSessionBreadcrumbs();
        OnPropertyChanged(nameof(ActiveNode));
        OnPropertyChanged(nameof(HasActiveNode));
        return node;
    }

    public GeneratorNode OpenChildNode(GeneratorNode parent, GeneratorNodeKind kind, object state)
    {
        var node = _navigator.CreateChild(parent, kind, state);
        _navigator.OpenNode(node.Id);
        RefreshSessionBreadcrumbs();
        OnPropertyChanged(nameof(ActiveNode));
        OnPropertyChanged(nameof(HasActiveNode));
        return node;
    }

    public void NavigateToNode(Guid nodeId)
    {
        _navigator.OpenNode(nodeId);
        RefreshSessionBreadcrumbs();
        OnPropertyChanged(nameof(ActiveNode));
        OnPropertyChanged(nameof(HasActiveNode));
    }

    private void NavigateToParent()
    {
        if (!CanNavigateToParent)
        {
            return;
        }

        _navigator.OpenParent();
        RefreshSessionBreadcrumbs();
        OnPropertyChanged(nameof(ActiveNode));
        OnPropertyChanged(nameof(HasActiveNode));
    }

    private void CompleteChildNode()
    {
        if (!CanCompleteChildNode || ActiveNode is null) return;

        try
        {
            var definition = _definitionCatalog.GetDefinition(ActiveNode.Kind);
            var validation = definition.Validate(ActiveNode, _generationSession);
            if (validation.IsValid)
            {
                ActiveNode.Status = GeneratorNodeStatus.Valid;
                NavigateToParent();
            }
        }
        catch
        {
            // Validation failed, stay on node
        }
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationSession.ActiveNode))
        {
            SyncActiveSessionToActiveNode();
        }
    }

    private void SyncActiveSessionToActiveNode()
    {
        var node = _generationSession.ActiveNode;

        if (node is null)
        {
            _activeSession = null;
            RefreshState();
            return;
        }

        if (_rootSession is IGeneratorNodeEditorViewModel rootEditor && rootEditor.Node?.Id == node.Id)
        {
            _activeSession = _rootSession;
            // Refresh pickers from session artifacts when returning to root
            if (_rootSession is IWorkspaceAwareGeneratorSessionViewModel workspaceAware)
            {
                workspaceAware.UpdateWorkspace(_lastProjectContext);
            }
            RefreshState();
            return;
        }

        if (!_nodeEditors.TryGetValue(node.Id, out var editor))
        {
            try
            {
                var definition = _definitionCatalog.GetDefinition(node.Kind);
                var services = new GeneratorSessionServices(_navigator, _serviceProvider);
                var created = definition.CreateEditor(node, _generationSession, services);
                if (created is IGeneratorSessionViewModel sessionViewModel)
                {
                    editor = sessionViewModel;
                    _nodeEditors[node.Id] = editor;
                    SubscribeSession(editor);
                    var projectContext = _workspaceStore.State.ProjectContext;
                    UpdateWorkspace(editor, projectContext);
                }
            }
            catch
            {
                editor = null;
            }
        }

        _activeSession = editor;
        RefreshState();
    }

    private void CloseRoot()
    {
        var previousRootSession = _rootSession;
        UnsubscribeAllEditors();
        _nodeEditors.Clear();
        _rootSession = null;
        _activeSession = null;
        RefreshState();
        NotifyRootSessionChanged(previousRootSession, _rootSession);
    }

    private void RefreshState()
    {
        Breadcrumbs.Clear();

        var node = _generationSession.ActiveNode;
        if (node is not null)
        {
            var breadcrumbs = new List<GeneratorBreadcrumbItem>();
            var current = node;
            while (current is not null)
            {
                breadcrumbs.Add(new GeneratorBreadcrumbItem(current.Title, current.Id == node.Id));
                current = current.ParentId is not null
                    ? _generationSession.FindNode(current.ParentId.Value)
                    : null;
            }

            breadcrumbs.Reverse();
            foreach (var item in breadcrumbs)
            {
                Breadcrumbs.Add(item);
            }
        }

        RefreshSessionBreadcrumbs();

        OnPropertyChanged(nameof(ActiveSession));
        OnPropertyChanged(nameof(RootSession));
        OnPropertyChanged(nameof(HasActiveSession));
        OnPropertyChanged(nameof(HasRootSession));
        OnPropertyChanged(nameof(ActiveSessionTitle));
        OnPropertyChanged(nameof(ActiveSessionSummary));
        OnPropertyChanged(nameof(BreadcrumbText));
        OnPropertyChanged(nameof(HasParentNode));
        OnPropertyChanged(nameof(CanNavigateToParent));
        OnPropertyChanged(nameof(HasChildNode));
        OnPropertyChanged(nameof(CanCompleteChildNode));

        CloseRootCommand.NotifyCanExecuteChanged();
        NavigateToParentCommand.NotifyCanExecuteChanged();
        CompleteChildNodeCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSessionBreadcrumbs()
    {
        SessionBreadcrumbs.Clear();

        var node = _generationSession.ActiveNode;
        if (node is null)
        {
            return;
        }

        var breadcrumbs = new List<GeneratorBreadcrumbItem>();
        var current = node;
        while (current is not null)
        {
            breadcrumbs.Add(new GeneratorBreadcrumbItem(current.Title, current.Id == node.Id));
            current = current.ParentId is not null
                ? _generationSession.FindNode(current.ParentId.Value)
                : null;
        }

        breadcrumbs.Reverse();
        foreach (var item in breadcrumbs)
        {
            SessionBreadcrumbs.Add(item);
        }
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

            if (_rootSession is IWorkspaceAwareGeneratorSessionViewModel rootAware)
            {
                rootAware.UpdateWorkspace(state.ProjectContext);
            }

            foreach (var editor in _nodeEditors.Values)
            {
                if (editor is IWorkspaceAwareGeneratorSessionViewModel editorAware)
                {
                    editorAware.UpdateWorkspace(state.ProjectContext);
                }
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

    private void UnsubscribeAllEditors()
    {
        if (_rootSession is INotifyPropertyChanged rootNotifier)
        {
            rootNotifier.PropertyChanged -= OnTrackedSessionPropertyChanged;
        }

        foreach (var editor in _nodeEditors.Values)
        {
            if (editor is INotifyPropertyChanged editorNotifier)
            {
                editorNotifier.PropertyChanged -= OnTrackedSessionPropertyChanged;
            }
        }
    }

    private void NotifyRootSessionChanged(IRootGeneratorSessionViewModel? previousRootSession, IRootGeneratorSessionViewModel? currentRootSession)
    {
        if (!ReferenceEquals(previousRootSession, currentRootSession))
        {
            RootSessionChanged?.Invoke(currentRootSession);
        }
    }

    private static void UpdateWorkspace(IGeneratorSessionViewModel session, ProjectWorkspaceContext? workspaceContext)
    {
        if (session is IWorkspaceAwareGeneratorSessionViewModel workspaceAwareSession)
        {
            workspaceAwareSession.UpdateWorkspace(workspaceContext);
        }
    }
}