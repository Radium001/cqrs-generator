using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Collections;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class DtoRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
    IEmbeddedGeneratorSessionViewModel<NewDtoDraft>,
    IWorkspaceAwareGeneratorSessionViewModel
{
    private static readonly IReadOnlyList<string> DtoSuffixes = ["", GeneratorConstants.DtoSuffix];

    private readonly GenerationActionDescriptor? _actionDescriptor;
    private readonly IAddDtoPlanService _planService;
    private readonly IEmbeddedSessionHost _embeddedSessionHost;
    private readonly IAddDtoScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly bool _isStandalone;
    private readonly FeatureItemViewModel? _fixedFeature;
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingDtoName;

    public DtoRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IAddDtoPlanService planService,
        IEmbeddedSessionHost embeddedSessionHost,
        IAddDtoScenarioOutlineBuilder scenarioOutlineBuilder,
        bool isStandalone,
        FeatureItemViewModel? fixedFeature = null,
        NewDtoDraft? existingDraft = null)
    {
        _actionDescriptor = actionDescriptor;
        _planService = planService;
        _embeddedSessionHost = embeddedSessionHost;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _isStandalone = isStandalone;
        _fixedFeature = fixedFeature;

        SessionId = isStandalone
            ? $"root:add-dto:{Guid.NewGuid():N}"
            : $"child:create-dto:{Guid.NewGuid():N}";

        AvailableFeatures = new RuntimeItemCollection<FeatureItemViewModel>(f => f.Name);
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

        if (existingDraft is not null)
        {
            DtoNameCyclic.SelectedIndex = existingDraft.SuffixIndex;
            DtoNameCyclic.Text = existingDraft.BaseName;
            foreach (var property in existingDraft.Properties)
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

    public RuntimeItemCollection<FeatureItemViewModel> AvailableFeatures { get; }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => _scenarioOutlineBuilder.Build(CreateFormState());

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);
        return _planService.BuildPlan(workspaceContext, CreateFormState());
    }

    public NewDtoDraft BuildDraft()
    {
        return new NewDtoDraft(
            DtoNameCyclic.Text.Trim(),
            DtoNameCyclic.SelectedIndex,
            Parameters
                .Where(property => property.IsComplete)
                .Select(property => new PropertySpec(property.Type.Trim(), property.Name.Trim()))
                .ToArray());
    }

    partial void OnSelectedFeatureChanged(FeatureItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
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
        AvailableFeatures.ClearRuntime();

        if (project is null)
        {
            FeaturePicker.Items = AvailableFeatures;
            return;
        }

        AvailableFeatures.SetDiscovered(
            project.Features.Select(f => new FeatureItemViewModel(f.Name, f.RelativePath)));

        FeaturePicker.Items = AvailableFeatures;

        if (_fixedFeature is not null)
        {
            var match = AvailableFeatures.FirstOrDefault(f =>
                string.Equals(f.Name, _fixedFeature.Name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                _isSyncingFeature = true;
                SelectedFeature = match;
                FeaturePicker.LockSelection(match);
                _isSyncingFeature = false;
            }
            else
            {
                AvailableFeatures.AddRuntime(_fixedFeature);
                _isSyncingFeature = true;
                SelectedFeature = _fixedFeature;
                FeaturePicker.LockSelection(_fixedFeature);
                _isSyncingFeature = false;
            }
        }
        else
        {
            var match = AvailableFeatures.FirstOrDefault();
            if (match is not null)
            {
                _isSyncingFeature = true;
                SelectedFeature = match;
                _isSyncingFeature = false;
            }
        }

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

        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void AddParameter()
    {
        var property = new PropertyEntryViewModel("string", $"Property{Parameters.Count + 1}");
        property.PropertyChanged += OnPropertyEntryChanged;
        Parameters.Add(property);
    }

    private void RemoveParameter(PropertyEntryViewModel? property)
    {
        if (property is null) return;
        property.PropertyChanged -= OnPropertyEntryChanged;
        Parameters.Remove(property);
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OnPropertyEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private AddDtoFormState CreateFormState()
    {
        var subfolder = SubfolderPicker.SelectedItem?.IsCustom == true
            ? SubfolderPicker.SearchText
            : SubfolderPicker.SelectedRawItem as string;

        if (string.Equals(subfolder, "(root folder)", StringComparison.Ordinal))
            subfolder = null;

        return new AddDtoFormState(
            SelectedFeature?.Name,
            SelectedFeature?.RelativePath,
            DtoNameCyclic.FullText,
            Parameters
                .Where(p => p.IsComplete)
                .Select(p => new PropertySpec(p.Type.Trim(), p.Name.Trim()))
                .ToArray(),
            UpdateWebImports,
            subfolder);
    }
}
