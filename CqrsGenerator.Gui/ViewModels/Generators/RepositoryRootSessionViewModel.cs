using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Collections;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class RepositoryRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
    IEmbeddedGeneratorSessionViewModel<NewRepositoryDraft>,
    IWorkspaceAwareGeneratorSessionViewModel
{
    private readonly GenerationActionDescriptor? _actionDescriptor;
    private readonly IAddRepositoryPlanService _planService;
    private readonly IEmbeddedSessionHost _embeddedSessionHost;
    private readonly IAddRepositoryScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly IAddEntityPlanService _entityPlanService;
    private readonly IAddEntityScenarioOutlineBuilder _entityScenarioOutlineBuilder;
    private readonly EfEntityPreparationService _efEntityPreparationService;
    private readonly RuntimeItemCollection<EntityItemViewModel> _entities;
    private readonly bool _isStandalone;
    private readonly SessionArtifactRegistry? _artifactRegistry;
    private ProjectModel? _projectModel;
    private bool _isSyncingEntity;
    private EntityItemViewModel? _customEntityItemViewModel;

    public RepositoryRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IAddRepositoryPlanService planService,
        IEmbeddedSessionHost embeddedSessionHost,
        IAddRepositoryScenarioOutlineBuilder scenarioOutlineBuilder,
        IAddEntityPlanService entityPlanService,
        IAddEntityScenarioOutlineBuilder entityScenarioOutlineBuilder,
        EfEntityPreparationService efEntityPreparationService,
        bool isStandalone,
        NewRepositoryDraft? existingDraft = null,
        SessionArtifactRegistry? artifactRegistry = null)
    {
        _actionDescriptor = actionDescriptor;
        _planService = planService;
        _embeddedSessionHost = embeddedSessionHost;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _entityPlanService = entityPlanService;
        _entityScenarioOutlineBuilder = entityScenarioOutlineBuilder;
        _efEntityPreparationService = efEntityPreparationService;
        _isStandalone = isStandalone;
        _artifactRegistry = artifactRegistry;

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

        OpenCreateEntityCommand = new RelayCommand(OpenCreateEntity);
        AddCustomMethodCommand = new RelayCommand(AddCustomMethod);
        RemoveCustomMethodCommand = new RelayCommand<RepositoryMethodEditorViewModel>(RemoveCustomMethod);

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
        if (existingDraft is not null)
        {
            AddDependencyInjectionRegistration = existingDraft.AddDependencyInjectionRegistration;
            foreach (var method in existingDraft.Methods.Where(method => !RepositoryMethodCatalog.Presets.Any(p => p.Name == method.Name)))
            {
                var editor = new RepositoryMethodEditorViewModel(method.Name, method.ReturnType, method.Parameters, RemoveCustomMethodCommand);
                editor.PropertyChanged += OnCustomMethodChanged;
                CustomMethods.Add(editor);
            }

            foreach (var preset in MethodPresets)
            {
                preset.IsSelected = existingDraft.Methods.Any(method => method.Name == preset.Name);
            }

            if (existingDraft.CustomEntity is not null)
            {
                FinishCustomEntity(existingDraft.CustomEntity);
            }
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
        CustomEntity is not null ||
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

    public IRelayCommand OpenCreateEntityCommand { get; }

    public IRelayCommand AddCustomMethodCommand { get; }

    public IRelayCommand<RepositoryMethodEditorViewModel> RemoveCustomMethodCommand { get; }

    public bool HasCustomEntity => CustomEntity is not null;

    public bool ShowCreateEntityButton => !HasCustomEntity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private EntityItemViewModel? _selectedEntity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomEntity))]
    [NotifyPropertyChangedFor(nameof(ShowCreateEntityButton))]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private NewEntityDraft? _customEntity;

    [ObservableProperty]
    private ItemChipViewModel? _customEntityChip;

    [ObservableProperty]
    private bool _addDependencyInjectionRegistration = true;

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => _scenarioOutlineBuilder.Build(CreateFormState());

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);
        return _planService.BuildPlan(workspaceContext, CreateFormState());
    }

    public NewRepositoryDraft BuildDraft()
    {
        var entity = ResolveEntity() ?? throw new InvalidOperationException("Repository entity is not configured.");
        return new NewRepositoryDraft(
            entity.Name,
            entity.Namespace,
            CustomEntity is not null,
            CustomEntity,
            BuildMethods(entity.Name),
            AddDependencyInjectionRegistration);
    }

    private bool HasEntitySelection => ResolveEntity() is not null;

    private bool AreCustomMethodsComplete => CustomMethods.All(method => method.IsComplete);

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        var previousDisplayName = SelectedEntity?.DisplayName;

        _entities.ClearRuntime();
        _customEntityItemViewModel = null;
        CustomEntity = null;
        CustomEntityChip = null;
        OnPropertyChanged(nameof(CustomEntityChip));
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

        if (_artifactRegistry is not null)
        {
            foreach (var regEntity in _artifactRegistry.EntityInfos)
            {
                if (!allEntities.Any(e => e.Name == regEntity.Name))
                {
                    allEntities.Add(new EntityItemViewModel(regEntity.Name, regEntity.DisplayName, regEntity.Namespace, regEntity.RelativePath));
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
            if (SelectedEntity is not null)
            {
                if (_customEntityItemViewModel is not null)
                {
                    _entities.RemoveRuntime(_customEntityItemViewModel);
                    _customEntityItemViewModel = null;
                }

                CustomEntity = null;
                CustomEntityChip = null;
                EntityPicker.UnlockSelection();
            }
            _isSyncingEntity = false;
        }
    }

    private void OpenCreateEntity()
    {
        var session = new EntityRootSessionViewModel(
            actionDescriptor: null,
            _entityPlanService,
            _entityScenarioOutlineBuilder,
            _efEntityPreparationService,
            isStandalone: false,
            existingDraft: CustomEntity);
        _embeddedSessionHost.Open(session, FinishCustomEntity);
    }

    private void FinishCustomEntity(NewEntityDraft draft)
    {
        CustomEntity = draft;
        SelectedEntity = null;

        var entityVm = new EntityItemViewModel(
            draft.EntityName,
            draft.EntityName + " (new)",
            "Domain.Entities",
            null);
        _isSyncingEntity = true;
        _customEntityItemViewModel = entityVm;
        _entities.AddRuntime(entityVm);
        EntityPicker.LockSelection(entityVm);
        _isSyncingEntity = false;

        _artifactRegistry?.PublishEntity(
            draft.EntityName,
            draft,
            new EntityInfo(draft.EntityName, "", draft.EntityName, "Domain.Entities", draft.Subfolder));

        CustomEntityChip = new ItemChipViewModel(
            draft.EntityName,
            editAction: () =>
            {
                var session = new EntityRootSessionViewModel(
                    actionDescriptor: null,
                    _entityPlanService,
                    _entityScenarioOutlineBuilder,
                    _efEntityPreparationService,
                    isStandalone: false,
                    existingDraft: draft);
                _embeddedSessionHost.Open(session, FinishCustomEntity);
            },
            deleteAction: () =>
            {
                if (_customEntityItemViewModel is not null)
                {
                    _entities.RemoveRuntime(_customEntityItemViewModel);
                    _artifactRegistry?.RemoveEntity(_customEntityItemViewModel.Name);
                }

                _customEntityItemViewModel = null;
                CustomEntity = null;
                CustomEntityChip = null;
                EntityPicker.UnlockSelection();
            });
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
            CustomEntity,
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
        if (CustomEntity is not null)
        {
            var subfolder = string.IsNullOrWhiteSpace(CustomEntity.Subfolder)
                ? null
                : CustomEntity.Subfolder.Replace('/', '.');
            return (CustomEntity.EntityName.Trim(), subfolder is null ? "Domain.Entities" : $"Domain.Entities.{subfolder}");
        }

        if (SelectedEntity is not null)
        {
            return (SelectedEntity.Name, SelectedEntity.Namespace);
        }

        return null;
    }

    partial void OnAddDependencyInjectionRegistrationChanged(bool value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
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
}
