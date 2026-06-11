using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class DtoRootSessionViewModel : ObservableObject,
    IRootGeneratorSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel,
    IGeneratorNodeEditorViewModel
{
    private static readonly IReadOnlyList<string> DtoSuffixes = ["", GeneratorConstants.DtoSuffix];

    private readonly GenerationActionDescriptor? _actionDescriptor;
    private readonly IAddDtoPlanService _planService;
    private readonly IAddDtoScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly bool _isStandalone;
    private readonly FeatureItemViewModel? _fixedFeature;
    private DtoGeneratorState? _sessionState;
    private GenerationSession? _generationSession;
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingDtoName;

    public GeneratorNode? Node { get; set; }

    public DtoRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IAddDtoPlanService planService,
        IAddDtoScenarioOutlineBuilder scenarioOutlineBuilder,
        bool isStandalone,
        FeatureItemViewModel? fixedFeature = null,
        GeneratorNode? node = null)
    {
        _actionDescriptor = actionDescriptor;
        _planService = planService;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _isStandalone = isStandalone;
        _fixedFeature = fixedFeature;
        Node = node;
        _sessionState = node?.State as DtoGeneratorState;

        SessionId = isStandalone
            ? $"root:add-dto:{Guid.NewGuid():N}"
            : $"child:create-dto:{Guid.NewGuid():N}";

        FeatureItems = [];
        FeaturePicker = new WrappedListPickerViewModel
        {
            AllowCustom = false,
            ItemNameSelector = item => item is FeatureItemViewModel f ? f.Name : item?.ToString() ?? string.Empty,
        };
        FeaturePicker.PropertyChanged += OnFeaturePickerChanged;

        DtoNameCyclic = new CyclicInputViewModel
        {
            Prefixes = [""],
            Suffixes = DtoSuffixes,
            SelectedIndex = 1,
        };
        DtoNameCyclic.PropertyChanged += OnDtoNameCyclicChanged;

        Parameters = [];
        AddParameterCommand = new RelayCommand(AddParameter);
        RemoveParameterCommand = new RelayCommand<PropertyEntryViewModel>(RemoveParameter);
        PropertyEditor = new PropertyEntryListEditorViewModel(
            Parameters,
            AddParameterCommand,
            RemoveParameterCommand,
            "Properties",
            "Add Property",
            "string",
            "Property1");

        Parameters.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        };

        SubfolderPicker = new WrappedListPickerViewModel
        {
        };
        SubfolderPickerConfiguration.Configure(SubfolderPicker);
        SubfolderPicker.Items = new List<string> { "(root folder)" };

        if (_sessionState is not null)
        {
            DtoNameCyclic.SelectedIndex = _sessionState.SuffixIndex;
            DtoNameCyclic.Text = _sessionState.BaseName;
            foreach (var property in _sessionState.Properties)
            {
                var propertyViewModel = new PropertyEntryViewModel(property.Type, property.Name);
                propertyViewModel.PropertyChanged += OnPropertyEntryChanged;
                Parameters.Add(propertyViewModel);
            }
        }
        else
        {
            AddParameter();
        }

        StatusText = _isStandalone ? "Configure Add DTO." : "Configure DTO draft.";
    }

    public string SessionId { get; }

    public string DisplayName => _isStandalone ? "Add DTO" : "Create DTO";

    public string Summary => _isStandalone
        ? "Configure DTO: name, properties, optional subfolder."
        : "Define draft DTO and return to parent generator.";

    public bool IsRoot => _isStandalone;

    public bool HasUnsavedChanges =>
        SelectedFeature is not null ||
        !string.IsNullOrWhiteSpace(DtoNameCyclic.Text) ||
        Parameters.Count > 0;

    public bool CanClose => true;

    public GenerationActionDescriptor? ActionDescriptor => _actionDescriptor;

    GenerationActionDescriptor IRootGeneratorSessionViewModel.ActionDescriptor =>
        _actionDescriptor ?? throw new InvalidOperationException("No action descriptor in embedded mode.");

    public bool CanBuildPlan =>
        _isStandalone &&
        SelectedFeature is not null &&
        !string.IsNullOrWhiteSpace(DtoName);

    public bool CanComplete =>
        !_isStandalone &&
        !string.IsNullOrWhiteSpace(DtoNameCyclic.Text) &&
        Parameters.All(property => property.IsComplete);

    public WrappedListPickerViewModel FeaturePicker { get; }

    public CyclicInputViewModel DtoNameCyclic { get; }

    public ObservableCollection<PropertyEntryViewModel> Parameters { get; }

    public PropertyEntryListEditorViewModel PropertyEditor { get; }

    public bool ShowSubfolderPicker => _isStandalone;

    public WrappedListPickerViewModel SubfolderPicker { get; }

    public IRelayCommand AddParameterCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemoveParameterCommand { get; }

    [ObservableProperty]
    private FeatureItemViewModel? _selectedFeature;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _dtoName = string.Empty;

    [ObservableProperty]
    private bool _updateWebImports = true;

    [ObservableProperty]
    private string _statusText = "Configure DTO.";

    public ObservableCollection<FeatureItemViewModel> FeatureItems { get; }

    public void SetGenerationSession(GenerationSession session)
    {
        _generationSession = session;
        _sessionState = Node?.State as DtoGeneratorState;
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        if (Node is null) return [];
        return ScenarioOutlineProjector.Project(Node);
    }

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    partial void OnSelectedFeatureChanged(FeatureItemViewModel? value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        var items = new List<string> { "(root folder)" };

        if (_projectModel is not null && value is not null)
        {
            items.AddRange(_projectModel.DtoSubfolders
                .Where(folder => string.Equals(folder.FeaturePath, value.RelativePath, StringComparison.OrdinalIgnoreCase))
                .Select(folder => folder.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase));
        }

        SubfolderPicker.Items = items;
    }

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;

        if (_generationSession is not null)
        {
            _generationSession.Artifacts.SetProjectModel(project);
        }

        if (project is null || _generationSession is null)
        {
            FeatureItems.Clear();
            FeaturePicker.Items = FeatureItems;
            return;
        }

        var selectedFeatureRef = _generationSession.References.GetRef(Node?.Id ?? default, "Feature") ?? _sessionState?.FeatureRef;
        var features = _generationSession.Artifacts.GetFeatures()
            .Select(feature => new FeatureItemViewModel(feature.Ref))
            .OrderBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FeatureItems.Clear();
        foreach (var feature in features)
        {
            FeatureItems.Add(feature);
        }

        FeaturePicker.Items = FeatureItems;

        var selected = selectedFeatureRef is not null
            ? FeatureItems.FirstOrDefault(feature => feature.Ref is not null && ArtifactRefEquals(feature.Ref, selectedFeatureRef))
            : null;

        if (selected is null && _fixedFeature is not null)
        {
            selected = FeatureItems.FirstOrDefault(feature =>
                string.Equals(feature.RelativePath, _fixedFeature.RelativePath, StringComparison.OrdinalIgnoreCase));
        }

        selected ??= FeatureItems.FirstOrDefault();

        var shouldLock = selected is not null && (
            _fixedFeature is not null ||
            (!_isStandalone && (_generationSession?.References.GetRef(Node?.Id ?? default, "Feature") is not null || _sessionState?.FeatureRef is not null)));

        _isSyncingFeature = true;
        SelectedFeature = selected;
        if (shouldLock)
        {
            FeaturePicker.LockSelection(selected);
        }
        else
        {
            FeaturePicker.SelectRawItem(selected);
        }
        _isSyncingFeature = false;

        _isSyncingDtoName = true;
        DtoName = DtoNameCyclic.FullText;
        _isSyncingDtoName = false;
    }

    private void OnFeaturePickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncingFeature) return;

        if (e.PropertyName == nameof(WrappedListPickerViewModel.SelectedItem))
        {
            _isSyncingFeature = true;
            SelectedFeature = FeaturePicker.SelectedItem?.OriginalItem as FeatureItemViewModel;
            _isSyncingFeature = false;
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void OnDtoNameCyclicChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncingDtoName) return;

        if (e.PropertyName == nameof(CyclicInputViewModel.FullText))
        {
            _isSyncingDtoName = true;
            DtoName = DtoNameCyclic.FullText;
            _isSyncingDtoName = false;
        }
    }

    partial void OnDtoNameChanged(string value)
    {
        if (_isSyncingDtoName) return;

        _isSyncingDtoName = true;
        DtoNameCyclic.Text = value;
        _isSyncingDtoName = false;

        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void AddParameter()
    {
        var property = new PropertyEntryViewModel("string", $"Property{Parameters.Count + 1}");
        property.PropertyChanged += OnPropertyEntryChanged;
        Parameters.Add(property);
        SyncToSessionState();
    }

    private void RemoveParameter(PropertyEntryViewModel? property)
    {
        if (property is null) return;
        property.PropertyChanged -= OnPropertyEntryChanged;
        Parameters.Remove(property);
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OnPropertyEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void SyncToSessionState()
    {
        if (_sessionState is null) return;
        _sessionState.BaseName = DtoNameCyclic.Text.Trim();
        _sessionState.SuffixIndex = DtoNameCyclic.SelectedIndex;
        _sessionState.Properties.Clear();
        foreach (var p in Parameters.Where(p => p.IsComplete))
        {
            _sessionState.Properties.Add(new PropertySpec(p.Type.Trim(), p.Name.Trim()));
        }
    }

    private static bool ArtifactRefEquals(ArtifactRef left, ArtifactRef right)
    {
        if (left.NodeId.HasValue && right.NodeId.HasValue)
        {
            return left.NodeId == right.NodeId;
        }

        return left.Kind == right.Kind
               && left.Origin == right.Origin
               && string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
               && string.Equals(left.FeaturePath, right.FeaturePath, StringComparison.OrdinalIgnoreCase);
    }
}
