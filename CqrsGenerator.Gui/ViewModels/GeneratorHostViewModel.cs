using CommunityToolkit.Mvvm.ComponentModel;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Services.Generators;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class GeneratorHostViewModel : ObservableObject
{
    private readonly IGeneratorCatalog _generatorCatalog;
    private readonly IWorkspaceSessionService _workspaceSessionService;

    public GeneratorHostViewModel(
        IWorkspaceStore workspaceStore,
        IGeneratorCatalog generatorCatalog,
        IWorkspaceSessionService workspaceSessionService)
    {
        _generatorCatalog = generatorCatalog;
        _workspaceSessionService = workspaceSessionService;
        Stack = new GeneratorStackViewModel(workspaceStore);
        ActionLauncher = new ActionLauncherViewModel(generatorCatalog, workspaceStore, OpenAction);

        Stack.PropertyChanged += (_, _) => RaiseStackStateChanged();
        Stack.RootSessionChanged += OnRootSessionChanged;
    }

    public GeneratorStackViewModel Stack { get; }

    public ActionLauncherViewModel ActionLauncher { get; }

    public string EditorTitle => Stack.ActiveSessionTitle;

    public string EditorSummary => Stack.ActiveSessionSummary;

    public bool HasActiveAction => Stack.HasActiveSession;

    public bool IsLauncherVisible => !HasActiveAction;

    public bool HasEmbeddedSession => Stack.HasEmbeddedSession;

    public string ActiveBreadcrumbText => Stack.BreadcrumbText;

    public string ActiveSessionSummary => Stack.ActiveSessionSummary;

    public IGeneratorSessionViewModel? ActiveSession => Stack.ActiveSession;

    public event Action<GenerationActionDescriptor?>? SelectedRootActionChanged;

    public event Action<IRootGeneratorSessionViewModel?>? RootSessionChanged;

    private void OpenAction(GenerationActionDescriptor action)
    {
        var rootSession = _generatorCatalog.GetDefinition(action.ActionId).CreateRootSession(Stack);
        Stack.OpenRoot(rootSession);
        _workspaceSessionService.SelectAction(action, rootSession);
        SelectedRootActionChanged?.Invoke(action);
        RaiseStackStateChanged();
    }

    private void OnRootSessionChanged(IRootGeneratorSessionViewModel? rootSession)
    {
        if (rootSession is null)
        {
            SelectedRootActionChanged?.Invoke(null);
        }

        RootSessionChanged?.Invoke(rootSession);
        RaiseStackStateChanged();
    }

    private void RaiseStackStateChanged()
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorSummary));
        OnPropertyChanged(nameof(HasActiveAction));
        OnPropertyChanged(nameof(IsLauncherVisible));
        OnPropertyChanged(nameof(HasEmbeddedSession));
        OnPropertyChanged(nameof(ActiveBreadcrumbText));
        OnPropertyChanged(nameof(ActiveSessionSummary));
        OnPropertyChanged(nameof(ActiveSession));
    }
}
