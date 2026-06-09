using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Collections;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class RepositoryRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel,
    IGeneratorNodeEditorViewModel
{
    private readonly GenerationActionDescriptor? _actionDescriptor;
    private readonly IAddRepositoryPlanService _planService;
    private readonly IAddRepositoryScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly RuntimeItemCollection<EntityItemViewModel> _entities;
    private readonly bool _isStandalone;
    private readonly RepositoryGeneratorState? _sessionState;
    private ProjectModel? _projectModel;
    private bool _isSyncingEntity;
    private GenerationSession? _generationSession;
    private IGenerationSessionNavigator? _navigator;

    private GeneratorNode? _node;
    public GeneratorNode? Node { get => _node; set => _node = value; }

    public RepositoryRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IAddRepositoryPlanService planService,
        IEmbeddedSessionHost embeddedSessionHost,
        IAddRepositoryScenarioOutlineBuilder scenarioOutlineBuilder,
        IAddEntityPlanService entityPlanService,
        IAddEntityScenarioOutlineBuilder entityScenarioOutlineBuilder,
        EfEntityPreparationService efEntityPreparationService,
        bool isStandalone,
        GeneratorNode? node = null)
    {
        _actionDescriptor = actionDescriptor;
        _planService = planService;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _isStandalone = isStandalone;
        _node = node;
        _sessionState = node?.State as RepositoryGeneratorState;

        SessionId = isStandalone
            ? $"root:add-repository:{Guid.NewGuid():N}"
            : $"child:create-repository:{Guid.NewGuid():N}";

        _entities = new RuntimeItemCollection<EntityItemViewModel>(e => e.DisplayName);
        EntityPicker = new WrappedListPickerViewModel
        {
            AllowCustom = false,
            ItemNameSelector = item => item is EntityItemViewModel entity ? entity.DisplayName : item?.ToString() ?? string.Empty,
        };
        EntityPicker.PropertyChanged += OnEntityPickerChanged;

        AddCustomMethodCommand = new RelayCommand(AddCustomMethod);
        RemoveCustomMethodCommand = new RelayCommand<RepositoryMethodEditorViewModel>(RemoveCustomMethod);
        OpenCreateEntityCommand = new RelayCommand(OpenCreateEntity, () => Node is not null);

        MethodPresets =
        [
            new RepositoryMethodPresetItemViewModel(RepositoryMethodCatalog.GetById.Key, RepositoryMethodCatalog.GetById.Name, RepositoryMethodCatalog.GetById.IsSelectedByDefault),
            new RepositoryMethodPresetItemViewModel(RepositoryMethodCatalog.Add.Key, RepositoryMethodCatalog.Add.Name, RepositoryMethodCatalog.Add.IsSelectedByDefault),
            new RepositoryMethodPresetItemViewModel(RepositoryMethodCatalog.Update.Key, RepositoryMethodCatalog.Update.Name, RepositoryMethodCatalog.Update.IsSelectedByDefault),
            new RepositoryMethodPresetItemViewModel(RepositoryMethodCatalog.Delete.Key, RepositoryMethodCatalog.Delete.Name, RepositoryMethodCatalog.Delete.IsSelectedByDefault),
        ];
        foreach (var preset in MethodPresets)
        {
            preset.PropertyChanged += OnMethodPresetChanged;
        }

        CustomMethods = [];
        if (_sessionState is not null)
        {
            AddDependencyInjectionRegistration = _sessionState.AddDependencyInjectionRegistration;
        }
    }

    public void SetGenerationSession(GenerationSession session, IGenerationSessionNavigator navigator)
    {
        _generationSession = session;
        _navigator = navigator;
        if (_node is null)
        {
            var state = new RepositoryGeneratorState { InterfaceName = "IRepository" };
            _node = navigator.CreateRoot(GeneratorNodeKind.Repository, state);
        }
    }

    public string SessionId { get; }

    public string DisplayName => _isStandalone ? "Add Repository" : "Create Repository";

    public string Summary => _isStandalone
        ? "Configure repository entity and methods."
        : "Define repository draft and return to the command generator.";

    public bool IsRoot => _isStandalone;

    public bool HasUnsavedChanges =>
        SelectedEntity is not null ||
        CustomMethods.Count > 0 ||
        !AddDependencyInjectionRegistration;

    public bool CanClose => true;

    public bool CanBuildPlan => _isStandalone && HasEntitySelection && AreCustomMethodsComplete;

    public bool CanComplete => !_isStandalone && HasEntitySelection && AreCustomMethodsComplete;

    public GenerationActionDescriptor? ActionDescriptor => _actionDescriptor;

    GenerationActionDescriptor IRootGeneratorSessionViewModel.ActionDescriptor =>
        _actionDescriptor ?? throw new InvalidOperationException("No action descriptor in embedded mode.");

    public WrappedListPickerViewModel EntityPicker { get; }

    public RuntimeItemCollection<EntityItemViewModel> AvailableEntities => _entities;

    public ObservableCollection<RepositoryMethodPresetItemViewModel> MethodPresets { get; }

    public ObservableCollection<RepositoryMethodEditorViewModel> CustomMethods { get; }

    public IRelayCommand AddCustomMethodCommand { get; }

    public IRelayCommand<RepositoryMethodEditorViewModel> RemoveCustomMethodCommand { get; }

    public IRelayCommand OpenCreateEntityCommand { get; }

    public bool ShowCreateEntityButton => Node is not null && _navigator is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private EntityItemViewModel? _selectedEntity;

    [ObservableProperty]
    private bool _addDependencyInjectionRegistration = true;

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        if (Node is null) return [];
        return ScenarioOutlineProjector.Project(Node);
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);
        return _planService.BuildPlan(workspaceContext, CreateFormState());
    }

    private bool HasEntitySelection => ResolveEntity() is not null;

    private bool AreCustomMethodsComplete => CustomMethods.All(method => method.IsComplete);

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        var previousDisplayName = SelectedEntity?.DisplayName;

        _entities.ClearRuntime();
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (project is null)
        {
            EntityPicker.RawItems = _entities;
            return;
        }

        var allEntities = project.Entities
            .OrderBy(entity => entity.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(entity => new EntityItemViewModel(entity.Name, entity.DisplayName, entity.Namespace, entity.RelativePath))
            .ToList();

        if (_generationSession is not null)
        {
            var sessionEntities = _generationSession.Artifacts.GetEntities();
            foreach (var sessionEntity in sessionEntities)
            {
                if (!allEntities.Any(e => e.Name == sessionEntity.Name))
                {
                    allEntities.Add(new EntityItemViewModel(sessionEntity.Name, sessionEntity.Name, "Domain.Entities", string.Empty));
                }
            }
        }

        _entities.SetDiscovered(allEntities);

        EntityPicker.RawItems = _entities;

        var match = _entities.FirstOrDefault(entity => entity.DisplayName == previousDisplayName)
                    ?? _entities.FirstOrDefault();
        _isSyncingEntity = true;
        SelectedEntity = match;
        EntityPicker.SelectRawItem(match);
        _isSyncingEntity = false;

        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OnEntityPickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WrappedListPickerViewModel.SelectedItem) && !_isSyncingEntity)
        {
            _isSyncingEntity = true;
            SelectedEntity = EntityPicker.SelectedRawItem as EntityItemViewModel;
            _isSyncingEntity = false;
        }
    }

    private void AddCustomMethod()
    {
        CustomMethods.Add(new RepositoryMethodEditorViewModel("GetActiveAsync", "Task", [], RemoveCustomMethodCommand));
        CustomMethods[^1].PropertyChanged += OnCustomMethodChanged;
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void RemoveCustomMethod(RepositoryMethodEditorViewModel? method)
    {
        if (method is null)
        {
            return;
        }

        method.PropertyChanged -= OnCustomMethodChanged;
        CustomMethods.Remove(method);
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private AddRepositoryFormState CreateFormState()
    {
        return new AddRepositoryFormState(
            SelectedEntity?.DisplayName,
            SelectedEntity?.Name,
            SelectedEntity?.Namespace,
            null,
            MethodPresets.Where(preset => preset.IsSelected).Select(preset => preset.Key).ToArray(),
            CustomMethods.Select(method => new RepositoryMethodSpec(
                method.Name,
                method.ReturnType,
                method.Parameters
                    .Where(parameter => parameter.IsComplete)
                    .Select(parameter => new PropertySpec(parameter.Type, parameter.Name))
                    .ToArray())).ToArray(),
            AddDependencyInjectionRegistration);
    }

    private IReadOnlyList<RepositoryMethodSpec> BuildMethods(string entityName)
    {
        return MethodPresets.Where(preset => preset.IsSelected)
            .Select(preset => RepositoryMethodCatalog.Create(preset.Key, entityName))
            .Concat(CustomMethods.Select(method => new RepositoryMethodSpec(
                method.Name.Trim(),
                method.ReturnType.Trim(),
                method.Parameters.Where(parameter => parameter.IsComplete)
                    .Select(parameter => new PropertySpec(parameter.Type.Trim(), parameter.Name.Trim()))
                    .ToArray())))
            .ToArray();
    }

    private (string Name, string Namespace)? ResolveEntity()
    {
        if (SelectedEntity is not null)
        {
            return (SelectedEntity.Name, SelectedEntity.Namespace);
        }

        return null;
    }

    partial void OnAddDependencyInjectionRegistrationChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void SyncToSessionState()
    {
        if (_sessionState is null) return;
        _sessionState.AddDependencyInjectionRegistration = AddDependencyInjectionRegistration;
    }

    private void OnMethodPresetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryMethodPresetItemViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void OnCustomMethodChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OpenCreateEntity()
    {
        if (Node is null || _navigator is null) return;
        var state = new EntityGeneratorState { EntityName = "NewEntity" };
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Entity, state);
        _navigator.OpenNode(child.Id);
    }
}