using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class CommandRootSessionViewModel : ObservableObject,
    IRootGeneratorSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel,
    IGeneratorNodeEditorViewModel
{
    private readonly IAddCommandPlanService _planService;
    private readonly IAddCommandScenarioOutlineBuilder _scenarioOutlineBuilder;
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingCommandName;
    private bool _isSyncingRepositorySelection;
    private CommandGeneratorState? _sessionState;
    private GenerationSession? _generationSession;
    private IGenerationSessionNavigator? _navigator;

    private GeneratorNode? _node;
    public GeneratorNode? Node { get => _node; set => _node = value; }

    public CommandRootSessionViewModel(
        GenerationActionDescriptor actionDescriptor,
        IAddCommandPlanService planService,
        IAddCommandScenarioOutlineBuilder scenarioOutlineBuilder,
        IAddRepositoryPlanService repositoryPlanService,
        IAddRepositoryScenarioOutlineBuilder repositoryScenarioOutlineBuilder,
        IAddEntityPlanService entityPlanService,
        IAddEntityScenarioOutlineBuilder entityScenarioOutlineBuilder,
        EfEntityPreparationService efEntityPreparationService,
        GeneratorNode? node = null)
    {
        _planService = planService;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _node = node;
        _sessionState = node?.State as CommandGeneratorState;
        ActionDescriptor = actionDescriptor;
        SessionId = $"root:{actionDescriptor.ActionId}";
        AvailableFeatures = [];
        Parameters = [];
        ResultTypeItems = [];

        FeaturePicker = new WrappedListPickerViewModel
        {
            ItemNameSelector = item => item is FeatureItemViewModel f ? f.Name : item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item is FeatureItemViewModel f && f.Ref is not null ? ArtifactKey.From(f.Ref).Value : item?.ToString() ?? string.Empty,
        };
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
        OpenCreateFeatureCommand = new RelayCommand(OpenCreateFeature, () => Node is not null && !HasOwnedCommittedChild("Feature", GeneratorNodeKind.Feature));
        EditSelectedFeatureCommand = new RelayCommand(EditSelectedFeature, () => SelectedFeature?.NodeId is not null);
        RemoveSelectedFeatureCommand = new RelayCommand(RemoveSelectedFeature, () => HasOwnedCommittedChild("Feature", GeneratorNodeKind.Feature));
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
        DependencyPicker.Picker.SelectionChanged += () =>
        {
            if (_isSyncingRepositorySelection)
            {
                return;
            }

            SyncSelectedRepositoriesToState();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanBuildPlan));
        };
        DependencyPicker.EditRequested = option =>
        {
            if (option.NodeId is Guid nodeId)
            {
                _navigator?.OpenNode(nodeId);
            }
        };
        DependencyPicker.RemoveRequested = option =>
        {
            if (option.NodeId is not Guid nodeId || _navigator is null)
            {
                return;
            }

            if (_navigator.RemoveNode(nodeId))
            {
                var toRemove = _sessionState?.RepositoryRefs.FirstOrDefault(r => r.NodeId == nodeId);
                if (toRemove is not null)
                {
                    _sessionState!.RepositoryRefs.Remove(toRemove);
                }
                RefreshRepositoryChoices();
            }
        };

        if (_sessionState is not null)
        {
            CommandNameCyclic.Text = _sessionState.CommandName;
            if (_sessionState.ResponseType is not null)
            {
                HasResponseType = true;
                ResultTypePicker.SearchText = _sessionState.ResponseType;
            }
            foreach (var param in _sessionState.Parameters)
            {
                var parameter = new PropertyEntryViewModel(param.Type, param.Name);
                parameter.PropertyChanged += OnParameterChanged;
                Parameters.Add(parameter);
            }
        }
    }

    public void SetGenerationSession(GenerationSession session, IGenerationSessionNavigator navigator)
    {
        _generationSession = session;
        _navigator = navigator;
        if (_node is null)
        {
            var state = new CommandGeneratorState { CommandName = "Create" };
            _node = navigator.CreateRoot(GeneratorNodeKind.Command, state);
            _sessionState = state;
        }
        else if (_node.State is CommandGeneratorState existingState)
        {
            _sessionState = existingState;
        }
        DependencyPicker.CreateRepositoryCommand = new RelayCommand(OpenCreateRepository, () => Node is not null);
        DependencyPicker.HasCreateRepository = true;
        NotifyFeatureCommands();
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
        DependencyPicker.GetSelected().Count > 0;

    public bool CanClose => true;

    public bool CanBuildPlan =>
        SelectedFeature is not null &&
        !string.IsNullOrWhiteSpace(CommandName) &&
        (!HasResponseType || !string.IsNullOrWhiteSpace(GetDtoTypeName()));

    public ObservableCollection<FeatureItemViewModel> AvailableFeatures { get; }

    public ObservableCollection<DtoItemViewModel> ResultTypeItems { get; }

    public ObservableCollection<PropertyEntryViewModel> Parameters { get; }

    public IRelayCommand AddParameterCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemoveParameterCommand { get; }

    public IRelayCommand OpenCreateFeatureCommand { get; }

    public IRelayCommand EditSelectedFeatureCommand { get; }

    public IRelayCommand RemoveSelectedFeatureCommand { get; }

    public bool ShowCreateFeatureButton => Node is not null && _navigator is not null && !HasOwnedCommittedChild("Feature", GeneratorNodeKind.Feature);

    public bool CanEditSelectedFeature => SelectedFeature?.NodeId is not null;

    public bool CanRemoveSelectedFeature => HasOwnedCommittedChild("Feature", GeneratorNodeKind.Feature);

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

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        if (Node is null) return [];
        return ScenarioOutlineProjector.Project(Node);
    }

    partial void OnSelectedFeatureChanged(FeatureItemViewModel? value)
    {
        if (_isSyncingFeature)
        {
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            return;
        }

        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        _isSyncingFeature = true;
        FeaturePicker.SelectRawItem(value);
        _isSyncingFeature = false;

        if (_projectModel is null || value is null)
        {
            ResultTypeItems.Clear();
            ResultTypePicker.RawItems = ResultTypeItems;
            DependencyPicker.SetDiscovered(Array.Empty<AvailableArtifactItem>());
            return;
        }

        var discoveredDtos = _projectModel.Dtos
            .Where(dto => string.Equals(dto.FeaturePath, value.RelativePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(dto => dto.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(dto => new DtoItemViewModel(dto.Name, dto.DisplayName, dto.Namespace))
            .ToList();

        if (_generationSession is not null)
        {
            foreach (var sessionDto in _generationSession.Artifacts.GetDtos(value.RelativePath))
            {
                if (!discoveredDtos.Any(d => d.Name == sessionDto.Name))
                {
                    discoveredDtos.Add(new DtoItemViewModel(sessionDto.Name, sessionDto.DisplayName, sessionDto.Ref.Namespace ?? string.Empty));
                }
            }
        }

        ResultTypeItems.Clear();
        foreach (var dto in discoveredDtos)
        {
            ResultTypeItems.Add(dto);
        }
        ResultTypePicker.RawItems = ResultTypeItems;
        RefreshRepositoryChoices();
        NotifyFeatureCommands();
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

        SyncToSessionState();
    }

    partial void OnHasResponseTypeChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }


    partial void OnUpdateWebImportsChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        var previousFeatureRef = _sessionState?.FeatureRef;

        AvailableFeatures.Clear();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (project is null)
        {
            SelectedFeature = null;
            StatusText = "Open a project first.";
            return;
        }

        var features = BuildFeatureItems(project);

        foreach (var feature in features)
        {
            AvailableFeatures.Add(feature);
        }

        FeaturePicker.Items = features;

        var match = features.FirstOrDefault(feature =>
            previousFeatureRef is not null &&
            feature.Ref is not null &&
            ArtifactRefEquals(feature.Ref, previousFeatureRef))
            ?? features.FirstOrDefault();

        SelectedFeature = match;

        _isSyncingCommandName = true;
        CommandName = CommandNameCyclic.FullText;
        _isSyncingCommandName = false;

        if (SelectedFeature is null)
        {
            ResultTypeItems.Clear();
            ResultTypePicker.RawItems = ResultTypeItems;
            RefreshRepositoryChoices();
        }

        StatusText = AvailableFeatures.Count == 0
            ? "No features discovered."
            : "Configure Add Command.";

        SyncToSessionState();
        NotifyFeatureCommands();
    }

    private void OnCommandNameCyclicChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CyclicInputViewModel.FullText) && !_isSyncingCommandName)
        {
            _isSyncingCommandName = true;
            CommandName = CommandNameCyclic.FullText;
            _isSyncingCommandName = false;
            SyncToSessionState();
        }
    }

    private void OnFeaturePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WrappedListPickerViewModel.SelectedItem) && !_isSyncingFeature)
        {
            SelectedFeature = FeaturePicker.SelectedRawItem as FeatureItemViewModel;
        }
    }

    private void OnResultTypePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WrappedListPickerViewModel.SelectedItem)
            or nameof(WrappedListPickerViewModel.SearchText)
            or nameof(WrappedListPickerViewModel.SelectedBaseName))
        {
            SyncToSessionState();
            OnPropertyChanged(nameof(CanBuildPlan));
        }
    }

    private void SyncToSessionState()
    {
        if (_sessionState is null) return;
        _sessionState.CommandName = CommandName.Trim();
        _sessionState.ResponseType = HasResponseType ? GetDtoTypeName() : null;
        _sessionState.UpdateWebImports = UpdateWebImports;
        _sessionState.Parameters.Clear();
        foreach (var p in Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Type) && !string.IsNullOrWhiteSpace(p.Name)))
        {
            _sessionState.Parameters.Add(new PropertySpec(p.Type.Trim(), p.Name.Trim()));
        }
        _sessionState.FeatureRef = SelectedFeature?.Ref;
        SyncSelectedRepositoriesToState();
    }

    private string? GetDtoTypeName()
    {
        return ResultTypePicker.SelectedBaseName;
    }

    private void AddParameter()
    {
        var parameter = new PropertyEntryViewModel("string", $"param{Parameters.Count + 1}");
        parameter.PropertyChanged += OnParameterChanged;
        Parameters.Add(parameter);
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void RemoveParameter(PropertyEntryViewModel? parameter)
    {
        if (parameter is null)
        {
            return;
        }

        parameter.PropertyChanged -= OnParameterChanged;
        Parameters.Remove(parameter);
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
    }


    private void OnParameterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PropertyEntryViewModel.Type) or nameof(PropertyEntryViewModel.Name))
        {
            SyncToSessionState();
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void OpenCreateFeature()
    {
        if (Node is null || _navigator is null || _generationSession is null) return;
        var state = new FeatureGeneratorState { FeatureName = "NewFeature", CreateWebFeature = true };
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Feature, state, "Feature");
        _navigator.OpenNode(child.Id);
    }

    private void EditSelectedFeature()
    {
        if (SelectedFeature?.NodeId is Guid nodeId)
        {
            _navigator?.OpenNode(nodeId);
        }
    }

    private void RemoveSelectedFeature()
    {
        var child = FindOwnedCommittedChild("Feature", GeneratorNodeKind.Feature);
        if (child is null || _navigator is null)
        {
            return;
        }

        if (_navigator.RemoveNode(child.Id))
        {
            if (_sessionState?.FeatureRef?.NodeId == child.Id)
            {
                _sessionState.FeatureRef = null;
            }

            FeaturePicker.UnlockSelection(clearSearchText: true);
            ReloadProject(_projectModel);
            NotifyFeatureCommands();
        }
    }

    private void OpenCreateRepository()
    {
        if (Node is null || _navigator is null || _generationSession is null) return;
        var state = new RepositoryGeneratorState { InterfaceName = "IRepository" };
        if (SelectedFeature?.Ref is not null)
        {
            state.FeatureRef = SelectedFeature.Ref;
        }
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Repository, state, "Repository");
        _navigator.OpenNode(child.Id);
    }

    private void RefreshRepositoryChoices()
    {
        if (_projectModel is null || SelectedFeature is null)
        {
            DependencyPicker.SetDiscovered(Array.Empty<AvailableArtifactItem>());
            return;
        }

        var featurePath = SelectedFeature.RelativePath;
        var selectedReferences = (_sessionState?.RepositoryRefs ?? []).ToList();
        _isSyncingRepositorySelection = true;
        try
        {
            if (_generationSession is not null)
            {
                var artifacts = _generationSession.Artifacts.GetRepositories(featurePath);
                DependencyPicker.SetDiscovered(artifacts);
                DependencyPicker.SetSelectedDependencies(selectedReferences, _sessionState?.StandardDependencyNames ?? Enumerable.Empty<string>());
            }
            else
            {
                DependencyPicker.SetDiscovered(_projectModel.Repositories);
                DependencyPicker.SetSelectedDependencies(selectedReferences, _sessionState?.StandardDependencyNames ?? Enumerable.Empty<string>());
            }
        }
        finally
        {
            _isSyncingRepositorySelection = false;
        }

        SyncSelectedRepositoriesToState();
    }

    private void SyncSelectedRepositoriesToState()
    {
        if (_sessionState is null)
        {
            return;
        }

        _sessionState.RepositoryRefs.Clear();
        _sessionState.StandardDependencyNames.Clear();
        foreach (var option in DependencyPicker.GetSelectedOptions())
        {
            if (option.Ref is not null)
            {
                _sessionState.RepositoryRefs.Add(option.Ref);
            }
            else if (!string.IsNullOrWhiteSpace(option.InterfaceName))
            {
                _sessionState.StandardDependencyNames.Add(option.InterfaceName);
            }
        }
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private GeneratorNode? FindOwnedCommittedChild(string relationshipName, GeneratorNodeKind kind)
    {
        return Node?.Children.FirstOrDefault(child =>
            child.Kind == kind &&
            child.Lifecycle == GeneratorNodeLifecycle.Committed &&
            string.Equals(child.RelationshipName, relationshipName, StringComparison.Ordinal));
    }

    private bool HasOwnedCommittedChild(string relationshipName, GeneratorNodeKind kind)
    {
        return FindOwnedCommittedChild(relationshipName, kind) is not null;
    }

    private void NotifyFeatureCommands()
    {
        OnPropertyChanged(nameof(ShowCreateFeatureButton));
        OnPropertyChanged(nameof(CanEditSelectedFeature));
        OnPropertyChanged(nameof(CanRemoveSelectedFeature));
        OpenCreateFeatureCommand.NotifyCanExecuteChanged();
        EditSelectedFeatureCommand.NotifyCanExecuteChanged();
        RemoveSelectedFeatureCommand.NotifyCanExecuteChanged();
    }

    private List<FeatureItemViewModel> BuildFeatureItems(ProjectModel project)
    {
        var items = new List<FeatureItemViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in project.Features.OrderBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase))
        {
            var reference = new ArtifactRef(
                GeneratorNodeKind.Feature,
                ArtifactOrigin.Project,
                feature.Name,
                FeaturePath: feature.RelativePath,
                ProjectPath: feature.Path,
                DisplayName: feature.Name);
            if (seen.Add(reference.FeaturePath ?? reference.Name))
            {
                items.Add(new FeatureItemViewModel(reference));
            }
        }

        if (_generationSession is not null)
        {
            foreach (var feature in _generationSession.Artifacts.GetFeatures().OrderBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase))
            {
                var key = feature.Ref.NodeId.HasValue ? $"session:{feature.Ref.NodeId.Value:D}" : feature.FeaturePath ?? feature.Name;
                if (seen.Add(key))
                {
                    items.Add(new FeatureItemViewModel(feature.Ref));
                }
            }
        }

        return items;
    }

    private static bool ArtifactRefEquals(ArtifactRef left, ArtifactRef right)
    {
        return ArtifactKey.Equals(left, right);
    }
}
