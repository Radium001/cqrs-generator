using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Collections;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class CommandRootSessionViewModel : ObservableObject, IPlanBuildingRootSessionViewModel, IWorkspaceAwareGeneratorSessionViewModel
{
    private readonly IEmbeddedSessionHost _embeddedSessionHost;
    private readonly IAddCommandPlanService _planService;
    private readonly IAddCommandScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly IAddRepositoryPlanService _repositoryPlanService;
    private readonly IAddRepositoryScenarioOutlineBuilder _repositoryScenarioOutlineBuilder;
    private readonly IAddEntityPlanService _entityPlanService;
    private readonly IAddEntityScenarioOutlineBuilder _entityScenarioOutlineBuilder;
    private readonly EfEntityPreparationService _efEntityPreparationService;
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingCommandName;
    private readonly List<NewRepositoryDraft> _repositoryDrafts = [];
    private readonly SessionArtifactRegistry _artifactRegistry = new();
    private string? _editingInterfaceName;

    public CommandRootSessionViewModel(
        GenerationActionDescriptor actionDescriptor,
        IEmbeddedSessionHost embeddedSessionHost,
        IAddCommandPlanService planService,
        IAddCommandScenarioOutlineBuilder scenarioOutlineBuilder,
        IAddRepositoryPlanService repositoryPlanService,
        IAddRepositoryScenarioOutlineBuilder repositoryScenarioOutlineBuilder,
        IAddEntityPlanService entityPlanService,
        IAddEntityScenarioOutlineBuilder entityScenarioOutlineBuilder,
        EfEntityPreparationService efEntityPreparationService)
    {
        _embeddedSessionHost = embeddedSessionHost;
        _planService = planService;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _repositoryPlanService = repositoryPlanService;
        _repositoryScenarioOutlineBuilder = repositoryScenarioOutlineBuilder;
        _entityPlanService = entityPlanService;
        _entityScenarioOutlineBuilder = entityScenarioOutlineBuilder;
        _efEntityPreparationService = efEntityPreparationService;
        ActionDescriptor = actionDescriptor;
        SessionId = $"root:{actionDescriptor.ActionId}";
        AvailableFeatures = [];
        Parameters = [];
        ResultTypeItems = new RuntimeItemCollection<DtoItemViewModel>(dto => dto.Name);

        FeaturePicker = new WrappedListPickerViewModel();
        FeaturePicker.PropertyChanged += OnFeaturePickerChanged;

        CommandNameCyclic = new CyclicInputViewModel
        {
            Prefixes = ["", GeneratorConstants.CommandVerbPrefixes[0]],
            Suffixes = [""],
            SelectedIndex = 1,
        };
        CommandNameCyclic.PropertyChanged += OnCommandNameCyclicChanged;

        ResultTypePicker = new WrappedListPickerViewModel
        {
            Prefixes = [""],
            Suffixes = [""],
            AllowCustom = true,
            CustomEntryPrefix = "",
            CustomEntryIcon = "M27.3138 4.68622C28.8759 6.24832 28.8759 8.78098 27.3138 10.3431L12.5409 25.116C11.9001 25.7568 11.0972 26.2114 10.218 26.4312L5.63602 27.5767C4.90364 27.7598 4.24025 27.0964 4.42335 26.364L5.56885 21.782C5.78864 20.9028 6.24323 20.0999 6.88402 19.4591L21.6569 4.68622C23.219 3.12412 25.7517 3.12412 27.3138 4.68622ZM20.2426 8.92865L8.29824 20.8734C7.91376 21.2578 7.641 21.7396 7.50913 22.2671L6.76786 25.2322L9.73295 24.4909C10.2604 24.359 10.7422 24.0863 11.1267 23.7018L23.0706 11.7566L20.2426 8.92865ZM23.0712 6.10043L21.6566 7.51465L24.4846 10.3426L25.8996 8.92886C26.6806 8.14781 26.6806 6.88148 25.8996 6.10043C25.1185 5.31939 23.8522 5.31939 23.0712 6.10043Z",
            ItemNameSelector = item => item is DtoItemViewModel dto ? dto.Name : item?.ToString() ?? string.Empty,
        };
        ResultTypePicker.PropertyChanged += OnResultTypePickerChanged;

        AddParameterCommand = new RelayCommand(AddParameter);
        RemoveParameterCommand = new RelayCommand<PropertyEntryViewModel>(RemoveParameter);
        ParameterEditor = new PropertyEntryListEditorViewModel(
            Parameters,
            AddParameterCommand,
            RemoveParameterCommand,
            "Parameters",
            "Add Parameter",
            "string",
            "value");
        Parameters.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        };

        DependencyPicker = new DependencyPickerViewModel();
        DependencyPicker.CreateRepositoryCommand = new RelayCommand(OpenCreateRepository);
        DependencyPicker.HasCreateRepository = true;
        DependencyPicker.Picker.SelectionChanged += () =>
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanBuildPlan));
        };
    }

    public WrappedListPickerViewModel FeaturePicker { get; }

    public CyclicInputViewModel CommandNameCyclic { get; }

    public WrappedListPickerViewModel ResultTypePicker { get; }

    public PropertyEntryListEditorViewModel ParameterEditor { get; }

    public DependencyPickerViewModel DependencyPicker { get; }

    public string SessionId { get; }

    public GenerationActionDescriptor ActionDescriptor { get; }

    public string DisplayName => ActionDescriptor.DisplayName;

    public string Summary => "Configure Add Command.";

    public bool IsRoot => true;

    public bool HasUnsavedChanges =>
        SelectedFeature is not null ||
        !string.IsNullOrWhiteSpace(CommandName) ||
        Parameters.Count > 0 ||
        !HasResponseType ||
        !UpdateWebImports ||
        DependencyPicker.GetSelected().Count > 0 ||
        _repositoryDrafts.Count > 0;

    public bool CanClose => true;

    public bool CanBuildPlan =>
        SelectedFeature is not null &&
        !string.IsNullOrWhiteSpace(CommandName) &&
        (!HasResponseType || !string.IsNullOrWhiteSpace(GetDtoTypeName()));

    public ObservableCollection<FeatureItemViewModel> AvailableFeatures { get; }

    public RuntimeItemCollection<DtoItemViewModel> ResultTypeItems { get; }

    public ObservableCollection<PropertyEntryViewModel> Parameters { get; }

    public IRelayCommand AddParameterCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemoveParameterCommand { get; }

    [ObservableProperty]
    private FeatureItemViewModel? _selectedFeature;

    [ObservableProperty]
    private bool _hasResponseType;

    [ObservableProperty]
    private string _commandName = string.Empty;

    [ObservableProperty]
    private bool _updateWebImports = true;

    [ObservableProperty]
    private string _statusText = "Configure Add Command.";

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

    private string? _previousFeaturePath;

    partial void OnSelectedFeatureChanged(FeatureItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isSyncingFeature)
        {
            _isSyncingFeature = true;
            FeaturePicker.SelectRawItem(value);
            _isSyncingFeature = false;
        }

        if (_projectModel is null || value is null)
        {
            ResultTypeItems.SetDiscovered([]);
            ResultTypePicker.RawItems = ResultTypeItems;
            DependencyPicker.SetDiscovered([]);
            return;
        }

        if (_previousFeaturePath is not null &&
            !string.Equals(_previousFeaturePath, value.RelativePath, StringComparison.OrdinalIgnoreCase))
        {
            _artifactRegistry.ClearFeatureArtifacts();
        }
        _previousFeaturePath = value.RelativePath;

        var discoveredDtos = _projectModel.Dtos
            .Where(dto => string.Equals(dto.FeaturePath, value.RelativePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(dto => dto.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(dto => new DtoItemViewModel(dto.Name, dto.DisplayName, dto.Namespace))
            .ToList();

        foreach (var regDto in _artifactRegistry.DtoInfos)
        {
            if (!discoveredDtos.Any(d => d.Name == regDto.Name))
            {
                discoveredDtos.Add(new DtoItemViewModel(regDto.Name, regDto.DisplayName, regDto.Namespace));
            }
        }

        ResultTypeItems.SetDiscovered(discoveredDtos);
        ResultTypePicker.RawItems = ResultTypeItems;
        DependencyPicker.SetDiscovered(_projectModel.Repositories);
    }

    partial void OnCommandNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isSyncingCommandName)
        {
            _isSyncingCommandName = true;
            CommandNameCyclic.Text = value;
            _isSyncingCommandName = false;
        }
    }

    partial void OnHasResponseTypeChanged(bool value)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        var previousFeaturePath = SelectedFeature?.RelativePath;
        _previousFeaturePath = null;
        _artifactRegistry.ClearFeatureArtifacts();
        _repositoryDrafts.Clear();
        DependencyPicker.Picker.ClearRuntime();

        AvailableFeatures.Clear();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (project is null)
        {
            SelectedFeature = null;
            StatusText = "Open a project first.";
            return;
        }

        var features = project.Features
            .OrderBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase)
            .Select(feature => new FeatureItemViewModel(feature.Name, feature.RelativePath))
            .ToList();

        foreach (var feature in features)
        {
            AvailableFeatures.Add(feature);
        }

        FeaturePicker.Items = features;

        var match = features.FirstOrDefault(feature =>
            string.Equals(feature.RelativePath, previousFeaturePath, StringComparison.OrdinalIgnoreCase))
            ?? features.FirstOrDefault();

        _isSyncingFeature = true;
        SelectedFeature = match;
        FeaturePicker.SelectRawItem(match);
        _isSyncingFeature = false;

        _isSyncingCommandName = true;
        CommandName = CommandNameCyclic.FullText;
        _isSyncingCommandName = false;

        DependencyPicker.SetDiscovered(project.Repositories);

        StatusText = AvailableFeatures.Count == 0
            ? "No features discovered."
            : "Configure Add Command.";
    }

    private void OnCommandNameCyclicChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CyclicInputViewModel.FullText) && !_isSyncingCommandName)
        {
            _isSyncingCommandName = true;
            CommandName = CommandNameCyclic.FullText;
            _isSyncingCommandName = false;
        }
    }

    private void OnFeaturePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WrappedListPickerViewModel.SelectedItem) && !_isSyncingFeature)
        {
            _isSyncingFeature = true;
            SelectedFeature = FeaturePicker.SelectedRawItem as FeatureItemViewModel;
            _isSyncingFeature = false;
        }
    }

    private void OnResultTypePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WrappedListPickerViewModel.SelectedItem)
            or nameof(WrappedListPickerViewModel.SearchText)
            or nameof(WrappedListPickerViewModel.SelectedBaseName))
        {
            OnPropertyChanged(nameof(CanBuildPlan));
        }
    }

    private AddCommandFormState CreateFormState()
    {
        return new AddCommandFormState(
            SelectedFeature?.Name,
            SelectedFeature?.RelativePath,
            CommandName,
            HasResponseType ? GetDtoTypeName() : null,
            Parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Type) && !string.IsNullOrWhiteSpace(parameter.Name))
                .Select(parameter => new PropertySpec(parameter.Type.Trim(), parameter.Name.Trim()))
                .ToArray(),
            DependencyPicker.GetSelected(),
            _repositoryDrafts.ToArray(),
            UpdateWebImports);
    }

    private string? GetDtoTypeName()
    {
        return ResultTypePicker.SelectedBaseName;
    }

    private void AddParameter()
    {
        Parameters.Add(new PropertyEntryViewModel("string", $"param{Parameters.Count + 1}"));
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void RemoveParameter(PropertyEntryViewModel? parameter)
    {
        if (parameter is null)
        {
            return;
        }

        Parameters.Remove(parameter);
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void OpenCreateRepository()
    {
        var session = new RepositoryRootSessionViewModel(
            actionDescriptor: null,
            _repositoryPlanService,
            _embeddedSessionHost,
            _repositoryScenarioOutlineBuilder,
            _entityPlanService,
            _entityScenarioOutlineBuilder,
            _efEntityPreparationService,
            isStandalone: false,
            artifactRegistry: _artifactRegistry);
        _embeddedSessionHost.Open(session, FinishRepositoryDraft);
    }

    private void FinishRepositoryDraft(NewRepositoryDraft draft)
    {
        var removeName = _editingInterfaceName ?? draft.InterfaceName;
        _repositoryDrafts.RemoveAll(existing => string.Equals(existing.InterfaceName, removeName, StringComparison.Ordinal));
        _repositoryDrafts.Add(draft);
        _editingInterfaceName = null;

        if (!string.Equals(removeName, draft.InterfaceName, StringComparison.Ordinal))
        {
            DependencyPicker.Picker.RemoveRuntime(new CommandDependencyOption(removeName));
        }

        DependencyPicker.AddGeneratedDependency(
            draft.InterfaceName,
            isSelected: true,
            onEdit: _ => EditRepositoryDraft(draft.InterfaceName),
            onRemove: _ => RemoveRepositoryDraft(draft.InterfaceName));

        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void EditRepositoryDraft(string interfaceName)
    {
        var existing = _repositoryDrafts.FirstOrDefault(draft => string.Equals(draft.InterfaceName, interfaceName, StringComparison.Ordinal));
        if (existing is null)
        {
            return;
        }

        _editingInterfaceName = interfaceName;

        var session = new RepositoryRootSessionViewModel(
            actionDescriptor: null,
            _repositoryPlanService,
            _embeddedSessionHost,
            _repositoryScenarioOutlineBuilder,
            _entityPlanService,
            _entityScenarioOutlineBuilder,
            _efEntityPreparationService,
            isStandalone: false,
            existing,
            _artifactRegistry);
        _embeddedSessionHost.Open(session, FinishRepositoryDraft);
    }

    private void RemoveRepositoryDraft(string interfaceName)
    {
        _repositoryDrafts.RemoveAll(draft => string.Equals(draft.InterfaceName, interfaceName, StringComparison.Ordinal));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanBuildPlan));
    }
}
