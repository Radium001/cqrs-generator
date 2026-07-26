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

public sealed partial class EntityRootSessionViewModel : ObservableObject,
    IRootGeneratorSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel,
    IGeneratorNodeEditorViewModel
{
    private readonly GenerationActionDescriptor? _actionDescriptor;
    private readonly IAddEntityPlanService _planService;
    private readonly EfEntityPreparationService _efEntityPreparationService;
    private readonly bool _isStandalone;
    private EntityGeneratorState? _sessionState;
    private readonly Dictionary<string, EfEntityCandidate> _efEntityByName = new(StringComparer.Ordinal);
    private ProjectModel? _projectModel;
    private bool _isSyncingName;
    private bool _isLoadingFromState;
    private bool _isApplyingSuggestedEntityName;
    private bool _hasManualEntityNameOverride;

    public GeneratorNode? Node { get; set; }

    public EntityRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IAddEntityPlanService planService,
        EfEntityPreparationService efEntityPreparationService,
        bool isStandalone,
        GeneratorNode? node = null)
    {
        _actionDescriptor = actionDescriptor;
        _planService = planService;
        _efEntityPreparationService = efEntityPreparationService;
        _isStandalone = isStandalone;
        Node = node;
        _sessionState = node?.State as EntityGeneratorState;

        SessionId = isStandalone
            ? $"root:add-entity:{Guid.NewGuid():N}"
            : $"child:create-entity:{Guid.NewGuid():N}";

        EfEntityPicker = new WrappedListPickerViewModel
        {
            AllowCustom = false,
            ItemNameSelector = item => item is EfEntityCandidate candidate ? candidate.Name : item?.ToString() ?? string.Empty,
        };
        EfEntityPicker.PropertyChanged += OnEfEntityPickerChanged;

        EfPropertyPicker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item is EfEntityProperty property ? $"{property.EfType} {property.EfName}" : item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item is EfEntityProperty property ? property.EfName : item?.ToString() ?? string.Empty,
        };
        EfPropertyPicker.SelectionChanged += OnEfPropertySelectionChanged;

        SubfolderPicker = new WrappedListPickerViewModel
        {
        };
        SubfolderPickerConfiguration.Configure(SubfolderPicker);
        SubfolderPicker.Items = new List<string> { "(root folder)" };
        SubfolderPicker.PropertyChanged += OnSubfolderPickerChanged;

        EntityNameCyclic = new CyclicInputViewModel
        {
            Prefixes = [""],
            Suffixes = [""],
            SelectedIndex = 0,
        };
        EntityNameCyclic.PropertyChanged += OnEntityNameCyclicChanged;

        ManualProperties = [];
        AddPropertyCommand = new RelayCommand(AddProperty);
        RemovePropertyCommand = new RelayCommand<PropertyEntryViewModel>(RemoveProperty);
        PropertyEditor = new PropertyEntryListEditorViewModel(
            ManualProperties,
            AddPropertyCommand,
            RemovePropertyCommand,
            "Properties",
            "Add Property",
            "string",
            "Property1");

        if (_sessionState is not null)
        {
            LoadFromSessionState(_sessionState);
        }
        else
        {
            SelectedSourceMode = EntitySourceMode.Manual;
            AddProperty();
        }
    }

    private string? _pendingEfEntityName;
    private IReadOnlyList<string> _pendingEfPropertyNames = [];

    public string SessionId { get; }

    public string DisplayName => _isStandalone ? "Add Entity" : "Create Entity";

    public string Summary => _isStandalone
        ? "Configure domain entity, properties, and EF-backed options."
        : "Define entity draft and return to the parent generator.";

    public bool IsRoot => _isStandalone;

    public bool HasUnsavedChanges =>
        !string.IsNullOrWhiteSpace(EntityName) ||
        !string.IsNullOrWhiteSpace(GetSubfolder()) ||
        ManualProperties.Count > 0 ||
        SelectedSourceMode == EntitySourceMode.EfEntity ||
        GenerateFactoryMethod ||
        GenerateEfMapping;

    public bool CanClose => true;

    public GenerationActionDescriptor? ActionDescriptor => _actionDescriptor;

    GenerationActionDescriptor IRootGeneratorSessionViewModel.ActionDescriptor =>
        _actionDescriptor ?? throw new InvalidOperationException("No action descriptor in embedded mode.");

    public bool CanBuildPlan =>
        _isStandalone &&
        HasRequiredState &&
        AreEntriesComplete;

    public bool CanComplete =>
        !_isStandalone &&
        HasRequiredState &&
        AreEntriesComplete;

    public WrappedListPickerViewModel EfEntityPicker { get; }

    public MultiSelectListPickerViewModel EfPropertyPicker { get; }

    public WrappedListPickerViewModel SubfolderPicker { get; }

    public CyclicInputViewModel EntityNameCyclic { get; }

    public ObservableCollection<PropertyEntryViewModel> ManualProperties { get; }

    public PropertyEntryListEditorViewModel PropertyEditor { get; }

    public IRelayCommand AddPropertyCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemovePropertyCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(IsEfEntityMode))]
    [NotifyPropertyChangedFor(nameof(IsManualMode))]
    [NotifyPropertyChangedFor(nameof(UseEfEntity))]
    private EntitySourceMode _selectedSourceMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private EfEntityCandidate? _selectedEfEntity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private bool _renameEfIdentifierProperties = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildPlan))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _entityName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private bool _generateFactoryMethod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private bool _generateEfMapping;


    partial void OnGenerateFactoryMethodChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnGenerateEfMappingChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public bool IsEfEntityMode => SelectedSourceMode == EntitySourceMode.EfEntity;

    public bool IsManualMode => SelectedSourceMode == EntitySourceMode.Manual;

    public bool UseEfEntity
    {
        get => IsEfEntityMode;
        set => SelectedSourceMode = value ? EntitySourceMode.EfEntity : EntitySourceMode.Manual;
    }


    public void SetGenerationSession(GenerationSession session)
    {
        if (Node?.State is EntityGeneratorState state)
        {
            _sessionState = state;
            LoadFromSessionState(state);
            if (_isStandalone)
            {
                SyncToSessionState();
            }
        }
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        if (Node is null) return [];
        return ScenarioOutlineProjector.Project(Node);
    }

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        _projectModel = workspaceContext?.ProjectModel;

        var subfolders = new List<string> { "(root folder)" };
        if (_projectModel is not null)
        {
            subfolders.AddRange(_projectModel.Entities
                .Select(entity => entity.RelativePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)!);
        }

        SubfolderPicker.Items = subfolders;
        RestoreSubfolderSelection(_sessionState?.Subfolder);

        _efEntityByName.Clear();
        var candidates = workspaceContext?.Config is null
            ? []
            : _efEntityPreparationService.Discover(workspaceContext.Config);

        foreach (var candidate in candidates)
        {
            _efEntityByName[candidate.Name] = candidate;
        }

        EfEntityPicker.Items = candidates.ToList();

        if (_pendingEfEntityName is null && SelectedSourceMode == EntitySourceMode.Manual)
        {
            SelectedEfEntity = null;
            EfEntityPicker.SelectRawItem(null);
        }

        if (_pendingEfEntityName is not null && _efEntityByName.TryGetValue(_pendingEfEntityName, out var pendingCandidate))
        {
            SelectedEfEntity = pendingCandidate;
            EfEntityPicker.SelectRawItem(pendingCandidate);
            _pendingEfEntityName = null;
            ApplyEfEntitySelectionDefaults();

            foreach (var key in _pendingEfPropertyNames)
            {
                if (!EfPropertyPicker.SelectedKeys.Contains(key) && pendingCandidate.Properties.Any(property => property.EfName == key))
                {
                    EfPropertyPicker.ToggleItemCommand.Execute(pendingCandidate.Properties.First(property => property.EfName == key));
                }
            }

            _pendingEfPropertyNames = [];
        }
    }

    partial void OnSelectedSourceModeChanged(EntitySourceMode value)
    {
        OnPropertyChanged(nameof(IsEfEntityMode));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UseEfEntity));

        if (value == EntitySourceMode.Manual)
        {
            SelectedEfEntity = null;
            EfEntityPicker.SelectRawItem(null);
            EfPropertyPicker.DeselectAllCommand.Execute(null);
            EfPropertyPicker.SetDiscovered(Array.Empty<EfEntityProperty>());
            _hasManualEntityNameOverride = false;
            SyncToSessionState();
            return;
        }

        _hasManualEntityNameOverride = false;
        if (SelectedEfEntity is not null)
        {
            ApplySuggestedEntityName(SelectedEfEntity.Name);
        }

        SyncToSessionState();
    }

    partial void OnSelectedEfEntityChanged(EfEntityCandidate? value)
    {
        ApplyEfEntitySelectionDefaults();
        if (IsEfEntityMode && !_hasManualEntityNameOverride && !string.IsNullOrWhiteSpace(value?.Name))
        {
            ApplySuggestedEntityName(value.Name);
        }

        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnRenameEfIdentifierPropertiesChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnEntityNameChanged(string value)
    {
        if (IsEfEntityMode && !_isApplyingSuggestedEntityName && !_isSyncingName)
        {
            _hasManualEntityNameOverride = true;
        }

        if (!_isSyncingName)
        {
            _isSyncingName = true;
            EntityNameCyclic.Text = value;
            _isSyncingName = false;
        }

        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private bool HasRequiredState =>
        !string.IsNullOrWhiteSpace(EntityName) &&
        (SelectedSourceMode != EntitySourceMode.EfEntity || SelectedEfEntity is not null);

    private bool AreEntriesComplete => ManualProperties.All(property => property.IsComplete);


    private void OnSubfolderPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WrappedListPickerViewModel.SelectedItem) or nameof(WrappedListPickerViewModel.SearchText))
        {
            SyncToSessionState();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void OnEfEntityPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WrappedListPickerViewModel.SelectedItem))
        {
            SelectedEfEntity = EfEntityPicker.SelectedRawItem as EfEntityCandidate;
        }
    }

    private void OnEfPropertySelectionChanged()
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
    }

    private void OnEntityNameCyclicChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CyclicInputViewModel.FullText) && !_isSyncingName)
        {
            if (IsEfEntityMode && !_isApplyingSuggestedEntityName)
            {
                _hasManualEntityNameOverride = true;
            }

            _isSyncingName = true;
            EntityName = EntityNameCyclic.FullText;
            _isSyncingName = false;
        }
    }

    private void AddProperty()
    {
        AddProperty("string", $"Property{ManualProperties.Count + 1}");
    }

    private void AddProperty(string type, string name)
    {
        var property = new PropertyEntryViewModel(type, name);
        property.PropertyChanged += OnManualPropertyChanged;
        ManualProperties.Add(property);
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void RemoveProperty(PropertyEntryViewModel? property)
    {
        if (property is null)
        {
            return;
        }

        property.PropertyChanged -= OnManualPropertyChanged;
        ManualProperties.Remove(property);
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OnManualPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PropertyEntryViewModel.Type) or nameof(PropertyEntryViewModel.Name))
        {
            SyncToSessionState();
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void SyncToSessionState()
    {
        if (_sessionState is null || _isLoadingFromState) return;
        _sessionState.SourceMode = SelectedSourceMode;
        _sessionState.SelectedEfEntity = SelectedEfEntity;
        _sessionState.SelectedEfPropertyNames.Clear();
        foreach (var key in EfPropertyPicker.SelectedKeys)
        {
            _sessionState.SelectedEfPropertyNames.Add(key);
        }

        _sessionState.RenameEfIdentifierProperties = RenameEfIdentifierProperties;
        _sessionState.EntityName = EntityNameCyclic.FullText.Trim();
        _sessionState.Subfolder = GetSubfolder();
        _sessionState.GenerateFactoryMethod = GenerateFactoryMethod;
        _sessionState.GenerateEfMapping = GenerateEfMapping;
        _sessionState.Properties.Clear();
        foreach (var p in ManualProperties.Where(p => p.IsComplete))
        {
            _sessionState.Properties.Add(new PropertySpec(p.Type.Trim(), p.Name.Trim()));
        }
    }

    private void LoadFromSessionState(EntityGeneratorState state)
    {
        _isLoadingFromState = true;
        try
        {
            SelectedSourceMode = state.SourceMode;
            SelectedEfEntity = state.SelectedEfEntity;
            RenameEfIdentifierProperties = state.RenameEfIdentifierProperties;
            GenerateFactoryMethod = state.GenerateFactoryMethod;
            GenerateEfMapping = state.GenerateEfMapping;
            EntityNameCyclic.Text = state.EntityName;
            EntityName = EntityNameCyclic.FullText;
            ManualProperties.Clear();
            foreach (var property in state.Properties.ToArray())
            {
                var propertyViewModel = new PropertyEntryViewModel(property.Type, property.Name);
                propertyViewModel.PropertyChanged += OnManualPropertyChanged;
                ManualProperties.Add(propertyViewModel);
            }

            if (ManualProperties.Count == 0)
            {
                var propertyViewModel = new PropertyEntryViewModel("string", "Property1");
                propertyViewModel.PropertyChanged += OnManualPropertyChanged;
                ManualProperties.Add(propertyViewModel);
            }

            if (state.SelectedEfEntity is not null)
            {
                _pendingEfEntityName = state.SelectedEfEntity.Name;
                _pendingEfPropertyNames = state.SelectedEfPropertyNames.ToArray();
            }
        }
        finally
        {
            _isLoadingFromState = false;
        }
    }

    private void ApplyEfEntitySelectionDefaults()
    {
        if (SelectedEfEntity is null)
        {
            EfPropertyPicker.DeselectAllCommand.Execute(null);
            EfPropertyPicker.SetDiscovered(Array.Empty<EfEntityProperty>());
            return;
        }

        EfPropertyPicker.SetDiscovered(SelectedEfEntity.Properties);
    }

    private void ApplySuggestedEntityName(string entityName)
    {
        _isApplyingSuggestedEntityName = true;
        _hasManualEntityNameOverride = false;
        EntityName = entityName;
        _isApplyingSuggestedEntityName = false;
    }

    private PreparedEfEntitySelection BuildFinalPropertiesAndMappings()
    {
        if (SelectedSourceMode == EntitySourceMode.EfEntity && SelectedEfEntity is not null)
        {
            var prepared = _efEntityPreparationService.Prepare(
                SelectedEfEntity,
                EfPropertyPicker.SelectedKeys.ToArray(),
                RenameEfIdentifierProperties);

            var properties = prepared.Properties
                .Concat(ManualProperties
                    .Where(property => property.IsComplete)
                    .Select(property => new PropertySpec(property.Type.Trim(), property.Name.Trim())))
                .ToArray();

            return new PreparedEfEntitySelection(properties, prepared.EfMappingFields);
        }

        return new PreparedEfEntitySelection(
            ManualProperties
                .Where(property => property.IsComplete)
                .Select(property => new PropertySpec(property.Type.Trim(), property.Name.Trim()))
                .ToArray(),
            []);
    }


    private void RestoreSubfolderSelection(string? savedSubfolder)
    {
        if (string.IsNullOrWhiteSpace(savedSubfolder))
        {
            SubfolderPicker.SearchText = string.Empty;
            if (SubfolderPicker.Items.Count > 0)
            {
                SubfolderPicker.SelectRawItem(SubfolderPicker.Items[0]);
            }
            return;
        }

        var existing = SubfolderPicker.Items.Cast<string>()
            .FirstOrDefault(item => string.Equals(item, savedSubfolder, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SubfolderPicker.SearchText = string.Empty;
            SubfolderPicker.SelectRawItem(existing);
            return;
        }

        SubfolderPicker.SearchText = savedSubfolder;
    }

    private string? GetSubfolder()
    {
        var selected = SubfolderPicker.SelectedItem?.BaseName ?? SubfolderPicker.SearchText;
        if (string.IsNullOrWhiteSpace(selected) || selected == "(root folder)")
        {
            return null;
        }

        return selected.Trim();
    }
}
