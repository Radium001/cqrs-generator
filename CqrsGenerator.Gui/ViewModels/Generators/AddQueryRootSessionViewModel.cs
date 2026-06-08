using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Collections;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class AddQueryRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
    IEmbeddedGeneratorSessionViewModel<AddQueryFormState>,
    IWorkspaceAwareGeneratorSessionViewModel
{
    private static readonly IReadOnlyList<string> DtoSuffixes = ["", GeneratorConstants.DtoSuffix];
    private static readonly IReadOnlyList<(string Prefix, ResponseShape Shape)> PrefixShapeMap =
        [("", ResponseShape.Single), (GeneratorConstants.ListWrapperPrefix, ResponseShape.List), (GeneratorConstants.EnumerableWrapperPrefix, ResponseShape.Enumerable)];

    private readonly IEmbeddedSessionHost _embeddedSessionHost;
    private readonly IAddQueryPlanService _planService;
    private readonly IAddDtoPlanService? _addDtoPlanService;
    private readonly IAddDtoScenarioOutlineBuilder? _addDtoScenarioOutlineBuilder;
    private readonly IQueryServiceSuggestionService? _queryServiceSuggestionService;
    private readonly IAddQueryScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly bool _isStandalone;
    private readonly string? _fixedFeaturePath;
    private readonly SessionArtifactRegistry? _artifactRegistry;
    private readonly ICreateFeaturePlanService? _createFeaturePlanService;
    private readonly ICreateFeatureScenarioOutlineBuilder? _createFeatureScenarioOutlineBuilder;
    private static readonly IReadOnlyList<string> MethodNameSuffixes = ["", GeneratorConstants.AsyncSuffix];
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingQueryName;
    private QueryDtoChoiceViewModel? _customDtoViewModel;
    private ItemChipViewModel? _customChip;
    private QueryServiceSuggestion? _queryServiceSuggestion;
    private bool _isSyncingMethodName;
    private bool _methodNameAutoDerived = true;
    private FeatureItemViewModel? _customFeatureViewModel;
    private ItemChipViewModel? _customFeatureChip;

    public AddQueryRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IEmbeddedSessionHost embeddedSessionHost,
        IAddQueryPlanService planService,
        IAddDtoPlanService? addDtoPlanService,
        IAddDtoScenarioOutlineBuilder? addDtoScenarioOutlineBuilder,
        IQueryServiceSuggestionService? queryServiceSuggestionService,
        IAddQueryScenarioOutlineBuilder scenarioOutlineBuilder,
        string? fixedFeaturePath = null,
        SessionArtifactRegistry? artifactRegistry = null,
        ICreateFeaturePlanService? createFeaturePlanService = null,
        ICreateFeatureScenarioOutlineBuilder? createFeatureScenarioOutlineBuilder = null)
    {
        _embeddedSessionHost = embeddedSessionHost;
        _planService = planService;
        _addDtoPlanService = addDtoPlanService;
        _addDtoScenarioOutlineBuilder = addDtoScenarioOutlineBuilder;
        _queryServiceSuggestionService = queryServiceSuggestionService;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _isStandalone = actionDescriptor is not null;
        _fixedFeaturePath = fixedFeaturePath;
        _artifactRegistry = artifactRegistry;
        _createFeaturePlanService = createFeaturePlanService;
        _createFeatureScenarioOutlineBuilder = createFeatureScenarioOutlineBuilder;
        ActionDescriptor = actionDescriptor;
        SessionId = _isStandalone
            ? $"root:{actionDescriptor!.ActionId}"
            : $"child:create-query:{Guid.NewGuid():N}";
        FeatureItems = new RuntimeItemCollection<FeatureItemViewModel>(f => f.Name);
        Parameters = [];
        ResultTypeItems = new RuntimeItemCollection<QueryDtoChoiceViewModel>(dto => dto.Name);

        FeaturePicker = new WrappedListPickerViewModel
        {
            ItemNameSelector = item => item is FeatureItemViewModel f ? f.Name : item?.ToString() ?? string.Empty,
        };
        FeaturePicker.PropertyChanged += OnFeaturePickerChanged;

        ResultTypePicker = new WrappedListPickerViewModel
        {
            Prefixes = ["", GeneratorConstants.ListWrapperPrefix, GeneratorConstants.EnumerableWrapperPrefix],
            Suffixes = [GeneratorConstants.GenericWrapperSuffix],
            AllowCustom = true,
            CustomEntryPrefix = "",
            CustomEntryIcon = "M27.3138 4.68622C28.8759 6.24832 28.8759 8.78098 27.3138 10.3431L12.5409 25.116C11.9001 25.7568 11.0972 26.2114 10.218 26.4312L5.63602 27.5767C4.90364 27.7598 4.24025 27.0964 4.42335 26.364L5.56885 21.782C5.78864 20.9028 6.24323 20.0999 6.88402 19.4591L21.6569 4.68622C23.219 3.12412 25.7517 3.12412 27.3138 4.68622ZM20.2426 8.92865L8.29824 20.8734C7.91376 21.2578 7.641 21.7396 7.50913 22.2671L6.76786 25.2322L9.73295 24.4909C10.2604 24.359 10.7422 24.0863 11.1267 23.7018L23.0706 11.7566L20.2426 8.92865ZM23.0712 6.10043L21.6566 7.51465L24.4846 10.3426L25.8996 8.92886C26.6806 8.14781 26.6806 6.88148 25.8996 6.10043C25.1185 5.31939 23.8522 5.31939 23.0712 6.10043Z",
            ItemNameSelector = item => item is QueryDtoChoiceViewModel dto ? dto.Name : item?.ToString() ?? string.Empty,
            ItemSecondaryTextSelector = item => item is QueryDtoChoiceViewModel dto && dto.IsLocal ? $"{dto.OwnerQueryName}/" : null,
            ItemAccentKindSelector = item => item is QueryDtoChoiceViewModel dto && dto.IsLocal ? WrappedListAccentKind.Warning : WrappedListAccentKind.None,
            ItemSelectableSelector = item => item is not QueryDtoChoiceViewModel dto || dto.IsSelectable,
            ItemSelectionBlockedReasonSelector = item => item is QueryDtoChoiceViewModel dto ? dto.SelectionBlockedReason : null,
            ItemSearchTextSelector = item => item is QueryDtoChoiceViewModel dto
                ? dto.IsLocal && !string.IsNullOrWhiteSpace(dto.OwnerQueryName)
                    ? $"{dto.Name} {dto.OwnerQueryName} {dto.DisplayName}"
                    : $"{dto.Name} {dto.DisplayName}"
                : item?.ToString() ?? string.Empty,
        };
        ResultTypePicker.PropertyChanged += OnResultTypePickerChanged;

        QueryNameCyclic = new CyclicInputViewModel
        {
            Prefixes = ["", GeneratorConstants.QueryVerbPrefixes[0]],
            Suffixes = [""],
            SelectedIndex = 1,
        };
        QueryNameCyclic.PropertyChanged += OnQueryNameCyclicChanged;

        MethodNameCyclic = new CyclicInputViewModel
        {
            Prefixes = [""],
            Suffixes = MethodNameSuffixes,
            SelectedIndex = 1,
        };
        MethodNameCyclic.PropertyChanged += OnMethodNameCyclicChanged;

        AddParameterCommand = new RelayCommand(AddParameter);
        RemoveParameterCommand = new RelayCommand<PropertyEntryViewModel>(RemoveParameter);
        OpenCreateDtoCommand = new RelayCommand(OpenCreateDto);
        OpenCreateFeatureCommand = new RelayCommand(OpenCreateFeature);
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
    }

    public WrappedListPickerViewModel FeaturePicker { get; }

    public WrappedListPickerViewModel ResultTypePicker { get; }

    public CyclicInputViewModel QueryNameCyclic { get; }

    public CyclicInputViewModel MethodNameCyclic { get; }

    public string SessionId { get; }

    public GenerationActionDescriptor? ActionDescriptor { get; }

    GenerationActionDescriptor IRootGeneratorSessionViewModel.ActionDescriptor =>
        ActionDescriptor ?? throw new InvalidOperationException("No action descriptor in embedded mode.");

    public string DisplayName => _isStandalone ? ActionDescriptor!.DisplayName : "Create Query";

    public string Summary => _isStandalone
        ? "Configure Add Query."
        : "Define draft query and return to parent generator.";

    public bool IsRoot => _isStandalone;

    public bool HasUnsavedChanges =>
        SelectedFeature is not null ||
        !string.IsNullOrWhiteSpace(QueryName) ||
        !string.IsNullOrWhiteSpace(MethodNameCyclic.Text) ||
        Parameters.Count > 0 ||
        CustomDto is not null ||
        CustomFeature is not null ||
        !CreateQueryServiceMethod ||
        !GenerateHandlerBody ||
        !GenerateQueryServiceBody ||
        !UpdateWebImports;

    public bool CanClose => true;

    public bool CanBuildPlan =>
        _isStandalone &&
        SelectedFeature is not null &&
        !string.IsNullOrWhiteSpace(QueryName) &&
        SelectedDtoSelection is not null &&
        !SelectedDtoSelectionBlocked &&
        (!CreateQueryServiceMethod || _queryServiceSuggestion?.Mode != QueryServiceSuggestionMode.Blocked) &&
        (!CreateQueryServiceMethod || !string.IsNullOrWhiteSpace(GetMethodName()));

    public bool CanComplete =>
        !_isStandalone &&
        SelectedFeature is not null &&
        !string.IsNullOrWhiteSpace(QueryName) &&
        SelectedDtoSelection is not null &&
        !SelectedDtoSelectionBlocked;

    public RuntimeItemCollection<FeatureItemViewModel> FeatureItems { get; }

    public RuntimeItemCollection<QueryDtoChoiceViewModel> ResultTypeItems { get; }

    public ObservableCollection<PropertyEntryViewModel> Parameters { get; }

    public PropertyEntryListEditorViewModel ParameterEditor { get; }

    public bool HasCustomDto => CustomDto is not null;

    public bool ShowCreateDtoButton => !HasCustomDto;

    public bool HasCustomFeature => CustomFeature is not null;

    public bool ShowCreateFeatureButton => !HasCustomFeature;

    public ItemChipViewModel? CustomChip => _customChip;

    public ItemChipViewModel? CustomFeatureChip => _customFeatureChip;

    public string? CustomDtoName => GetFullDtoName();

    public int CustomDtoPropertiesCount => CustomDto?.Properties.Count ?? 0;

    public QueryDtoChoiceViewModel? SelectedDtoChoice => ResultTypePicker.SelectedRawItem as QueryDtoChoiceViewModel;

    public QueryDtoSelectionState? SelectedDtoSelection => CreateDtoSelectionState();

    public bool ShowDtoPromotionHint => SelectedDtoRequiresPromotion;

    public bool SelectedDtoRequiresPromotion => SelectedDtoChoice?.IsLocal == true && !HasCustomDto;

    public bool SelectedDtoSelectionBlocked => SelectedDtoChoice is { IsSelectable: false };

    public string DtoPromotionHintText => SelectedDtoChoice is { IsLocal: true, OwnerQueryName: not null }
        ? $"{SelectedDtoChoice.OwnerQueryName}/{SelectedDtoChoice.Name} -> DTOs/{SelectedDtoChoice.Name}"
        : string.Empty;

    public QueryServiceItemViewModel? AutoQueryService { get; private set; }

    public IRelayCommand AddParameterCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemoveParameterCommand { get; }

    public IRelayCommand OpenCreateDtoCommand { get; }

    public IRelayCommand OpenCreateFeatureCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomDto))]
    [NotifyPropertyChangedFor(nameof(ShowCreateDtoButton))]
    [NotifyPropertyChangedFor(nameof(CustomDtoName))]
    [NotifyPropertyChangedFor(nameof(CustomDtoPropertiesCount))]
    private NewDtoDraft? _customDto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomFeature))]
    [NotifyPropertyChangedFor(nameof(ShowCreateFeatureButton))]
    private NewFeatureDraft? _customFeature;

    [ObservableProperty]
    private FeatureItemViewModel? _selectedFeature;

    [ObservableProperty]
    private string _queryName = string.Empty;

    [ObservableProperty]
    private AutoItemStatusViewModel _queryServiceStatus = new();

    [ObservableProperty]
    private bool _generateHandlerBody = true;

    [ObservableProperty]
    private bool _createQueryServiceMethod = true;

    [ObservableProperty]
    private bool _generateQueryServiceBody = true;

    [ObservableProperty]
    private bool _updateWebImports = true;

    [ObservableProperty]
    private string _statusText = "Configure Add Query.";

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => _scenarioOutlineBuilder.Build(CreateFormState());

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);
        var plan = _planService.BuildPlan(workspaceContext, CreateFormState());

        if (CustomFeature is not null && _createFeaturePlanService is not null)
        {
            var featureFormState = new CreateFeatureFormState(
                CustomFeature.FeatureName,
                CustomFeature.Subfolder,
                CustomFeature.CreateWebFeature);
            var featurePlan = _createFeaturePlanService.BuildPlan(workspaceContext, featureFormState);
            plan.Merge(featurePlan);
        }

        return plan;
    }

    public AddQueryFormState BuildDraft()
    {
        return CreateFormState();
    }

    public void FinishCustomDto(NewDtoDraft draft)
    {
        CustomDto = draft;

        var fullName = GetFullDtoName(draft) ?? draft.BaseName;
        _customDtoViewModel = new QueryDtoChoiceViewModel(
            fullName,
            fullName + " (custom)",
            string.Empty,
            DtoLocationKind.LocalQueryDto,
            GetQueryBaseName(),
            isRuntime: true);
        ResultTypeItems.AddRuntime(_customDtoViewModel);
        ResultTypePicker.LockSelection(_customDtoViewModel);

        if (_artifactRegistry is not null && SelectedFeature is not null)
        {
            _artifactRegistry.PublishDto(
                fullName,
                draft,
                new DtoInfo(
                    fullName,
                    SelectedFeature.RelativePath,
                    string.Empty,
                    string.Empty,
                    fullName,
                    DtoLocationKind.LocalQueryDto,
                    GetQueryBaseName()));
        }

        _customChip = new ItemChipViewModel(
            fullName,
            editAction: () =>
            {
                var session = new DtoRootSessionViewModel(
                    actionDescriptor: null,
                    _addDtoPlanService!,
                    _embeddedSessionHost,
                    _addDtoScenarioOutlineBuilder!,
                    isStandalone: false,
                    fixedFeature: SelectedFeature,
                    existingDraft: draft);
                _embeddedSessionHost.Open<NewDtoDraft>(session, result =>
                {
                    FinishCustomDto(result);
                });
            },
            deleteAction: DeleteCustomDto);

        OnPropertyChanged(nameof(CustomChip));
        OnPropertyChanged(nameof(SelectedDtoSelection));
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void FinishCustomFeature(NewFeatureDraft draft)
    {
        var featurePath = draft.Subfolder is not null
            ? $"{draft.Subfolder}/{draft.FeatureName}"
            : draft.FeatureName;

        _customFeatureViewModel = new FeatureItemViewModel(
            draft.FeatureName,
            featurePath,
            isRuntime: true);

        FeatureItems.AddRuntime(_customFeatureViewModel);
        FeaturePicker.LockSelection(_customFeatureViewModel);

        _isSyncingFeature = true;
        SelectedFeature = _customFeatureViewModel;
        _isSyncingFeature = false;

        CustomFeature = draft;

        _customFeatureChip = new ItemChipViewModel(
            draft.FeatureName,
            editAction: () =>
            {
                var session = new CreateFeatureRootSessionViewModel(
                    actionDescriptor: null,
                    _createFeaturePlanService!,
                    _createFeatureScenarioOutlineBuilder!,
                    isStandalone: false,
                    existingDraft: draft);
                _embeddedSessionHost.Open<NewFeatureDraft>(session, result =>
                {
                    FinishCustomFeature(result);
                });
            },
            deleteAction: DeleteCustomFeature);

        OnPropertyChanged(nameof(CustomFeatureChip));
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

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
            SetQueryServiceSuggestion(null);
            return;
        }
        RefreshDtoChoices();

        if (_queryServiceSuggestionService is not null)
        {
            var suggestion = _queryServiceSuggestionService.Suggest(_projectModel, value.Name, value.RelativePath);
            SetQueryServiceSuggestion(suggestion);
        }
    }

    partial void OnQueryNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isSyncingQueryName)
        {
            _isSyncingQueryName = true;
            QueryNameCyclic.Text = value;
            _isSyncingQueryName = false;
        }

        if (_methodNameAutoDerived)
        {
            _isSyncingMethodName = true;
            MethodNameCyclic.Text = value;
            _isSyncingMethodName = false;
        }

        RefreshDtoChoices();
    }

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        _methodNameAutoDerived = true;
        _isSyncingMethodName = true;
        MethodNameCyclic.Text = QueryNameCyclic.FullText;
        _isSyncingMethodName = false;
        var previousFeaturePath = SelectedFeature?.RelativePath;

        FeatureItems.ClearRuntime();
        FeaturePicker.UnlockSelection();
        ResultTypeItems.ClearRuntime();
        ResultTypePicker.UnlockSelection();
        CustomDto = null;
        _customDtoViewModel = null;
        _customChip = null;
        CustomFeature = null;
        _customFeatureViewModel = null;
        _customFeatureChip = null;
        SetQueryServiceSuggestion(null);

        OnPropertyChanged(nameof(CustomChip));
        OnPropertyChanged(nameof(CustomFeatureChip));
        OnPropertyChanged(nameof(SelectedDtoSelection));
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

        FeatureItems.SetDiscovered(features);
        FeaturePicker.Items = FeatureItems;

        if (_fixedFeaturePath is not null)
        {
            var lockedMatch = features.FirstOrDefault(f =>
                string.Equals(f.RelativePath, _fixedFeaturePath, StringComparison.OrdinalIgnoreCase));
            if (lockedMatch is not null)
            {
                _isSyncingFeature = true;
                SelectedFeature = lockedMatch;
                FeaturePicker.LockSelection(lockedMatch);
                _isSyncingFeature = false;
            }
            else
            {
                _isSyncingFeature = true;
                SelectedFeature = null;
                FeaturePicker.SelectRawItem(null);
                _isSyncingFeature = false;
            }
        }
        else
        {
            var match = features.FirstOrDefault(feature =>
                string.Equals(feature.RelativePath, previousFeaturePath, StringComparison.OrdinalIgnoreCase))
                ?? features.FirstOrDefault();

            _isSyncingFeature = true;
            SelectedFeature = match;
            FeaturePicker.SelectRawItem(match);
            _isSyncingFeature = false;
        }

        _isSyncingQueryName = true;
        QueryName = QueryNameCyclic.FullText;
        _isSyncingQueryName = false;

        StatusText = features.Count == 0
            ? "No features discovered."
            : "Configure Add Query.";
    }

    private void OnQueryNameCyclicChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CyclicInputViewModel.FullText) && !_isSyncingQueryName)
        {
            _isSyncingQueryName = true;
            QueryName = QueryNameCyclic.FullText;
            _isSyncingQueryName = false;
        }
    }

    private void OnFeaturePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WrappedListPickerViewModel.SelectedItem) && !_isSyncingFeature)
        {
            var rawItem = FeaturePicker.SelectedRawItem;
            if (rawItem is FeatureItemViewModel feature)
            {
                _isSyncingFeature = true;
                SelectedFeature = feature;
                _isSyncingFeature = false;
            }
        }
    }

    private void OnMethodNameCyclicChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CyclicInputViewModel.FullText))
        {
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(HasUnsavedChanges));

            if (!_isSyncingMethodName)
            {
                _methodNameAutoDerived = false;
            }
        }
    }

    partial void OnCreateQueryServiceMethodChanged(bool value)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OnResultTypePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WrappedListPickerViewModel.SelectedPrefixIndex)
            or nameof(WrappedListPickerViewModel.SelectedItem)
            or nameof(WrappedListPickerViewModel.SearchText)
            or nameof(WrappedListPickerViewModel.SelectedBaseName))
        {
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(SelectedDtoChoice));
            OnPropertyChanged(nameof(SelectedDtoSelection));
            OnPropertyChanged(nameof(ShowDtoPromotionHint));
            OnPropertyChanged(nameof(DtoPromotionHintText));
            OnPropertyChanged(nameof(SelectedDtoRequiresPromotion));
            OnPropertyChanged(nameof(SelectedDtoSelectionBlocked));
        }
    }

    private void OpenCreateDto()
    {
        if (_addDtoPlanService is null || _addDtoScenarioOutlineBuilder is null)
        {
            return;
        }

        var createDtoSession = new DtoRootSessionViewModel(
            actionDescriptor: null,
            _addDtoPlanService,
            _embeddedSessionHost,
            _addDtoScenarioOutlineBuilder,
            isStandalone: false,
            fixedFeature: SelectedFeature,
            existingDraft: CustomDto);
        _embeddedSessionHost.Open<NewDtoDraft>(
            createDtoSession,
            FinishCustomDto);
    }

    private void OpenCreateFeature()
    {
        if (_createFeaturePlanService is null || _createFeatureScenarioOutlineBuilder is null)
        {
            return;
        }

        var createFeatureSession = new CreateFeatureRootSessionViewModel(
            actionDescriptor: null,
            _createFeaturePlanService,
            _createFeatureScenarioOutlineBuilder,
            isStandalone: false,
            existingDraft: CustomFeature);
        _embeddedSessionHost.Open<NewFeatureDraft>(
            createFeatureSession,
            FinishCustomFeature);
    }

    private void DeleteCustomDto()
    {
        if (_customDtoViewModel is not null)
        {
            ResultTypeItems.RemoveRuntime(_customDtoViewModel);
            ResultTypePicker.UnlockSelection();
            if (_artifactRegistry is not null)
            {
                _artifactRegistry.RemoveDto(_customDtoViewModel.Name);
            }
        }

        CustomDto = null;
        _customDtoViewModel = null;
        _customChip = null;

        OnPropertyChanged(nameof(CustomChip));
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void DeleteCustomFeature()
    {
        if (_customFeatureViewModel is not null)
        {
            FeatureItems.RemoveRuntime(_customFeatureViewModel);
            FeaturePicker.UnlockSelection();
        }

        CustomFeature = null;
        _customFeatureViewModel = null;
        _customFeatureChip = null;

        if (_isSyncingFeature)
        {
            _isSyncingFeature = false;
        }
        else
        {
            _isSyncingFeature = true;
            SelectedFeature = null;
            _isSyncingFeature = false;
        }

        OnPropertyChanged(nameof(CustomFeatureChip));
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void SetQueryServiceSuggestion(QueryServiceSuggestion? suggestion)
    {
        _queryServiceSuggestion = suggestion;

        if (suggestion is null)
        {
            AutoQueryService = null;
            QueryServiceStatus.DisplayText = string.Empty;
            QueryServiceStatus.Status = AutoItemStatus.Created;
        }
        else
        {
            AutoQueryService = new QueryServiceItemViewModel(
                suggestion.InterfaceName,
                suggestion.ImplementationName,
                suggestion.InterfacePath ?? string.Empty,
                suggestion.ImplementationPath);
            QueryServiceStatus.DisplayText = suggestion.InterfaceName;
            QueryServiceStatus.Status = suggestion.Status;
        }

        OnPropertyChanged(nameof(AutoQueryService));
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void RefreshDtoChoices()
    {
        if (_projectModel is null || SelectedFeature is null)
        {
            ResultTypeItems.SetDiscovered([]);
            ResultTypePicker.RawItems = ResultTypeItems;
            return;
        }

        var featurePath = SelectedFeature.RelativePath;
        var queryBaseName = GetQueryBaseName();
        var discoveredDtos = _projectModel.Dtos
            .Where(dto => string.Equals(dto.FeaturePath, featurePath, StringComparison.OrdinalIgnoreCase))
            .Where(dto => dto.LocationKind == DtoLocationKind.SharedFeatureDto
                || !string.Equals(dto.OwnerQueryName, queryBaseName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(dto => dto.LocationKind == DtoLocationKind.SharedFeatureDto ? 0 : 1)
            .ThenBy(dto => dto.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(dto => new QueryDtoChoiceViewModel(
                dto.Name,
                dto.DisplayName,
                dto.Namespace,
                dto.LocationKind,
                dto.OwnerQueryName,
                dto.Path,
                isSelectable: dto.LocationKind == DtoLocationKind.SharedFeatureDto || !HasSharedConflict(dto, featurePath),
                selectionBlockedReason: dto.LocationKind == DtoLocationKind.LocalQueryDto && HasSharedConflict(dto, featurePath)
                    ? "A shared DTO with the same name already exists."
                    : null))
            .ToList();

        if (_artifactRegistry is not null)
        {
            foreach (var regDto in _artifactRegistry.DtoInfos)
            {
                if (!discoveredDtos.Any(d => d.Name == regDto.Name))
                {
                    discoveredDtos.Add(new QueryDtoChoiceViewModel(
                        regDto.Name,
                        regDto.DisplayName,
                        regDto.Namespace,
                        regDto.LocationKind,
                        regDto.OwnerQueryName,
                        regDto.Path,
                        isRuntime: true));
                }
            }
        }

        ResultTypeItems.SetDiscovered(discoveredDtos);
        ResultTypePicker.RawItems = ResultTypeItems;

        if (_customDtoViewModel is not null)
        {
            ResultTypePicker.SelectRawItem(_customDtoViewModel);
        }
    }

    private AddQueryFormState CreateFormState()
    {
        return new AddQueryFormState(
            SelectedFeature?.Name,
            SelectedFeature?.RelativePath,
            QueryName,
            CreateDtoSelectionState(),
            CustomDto,
            GetShapeFromPrefixIndex(ResultTypePicker.SelectedPrefixIndex),
            Parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Type) && !string.IsNullOrWhiteSpace(parameter.Name))
                .Select(parameter => new PropertySpec(parameter.Type.Trim(), parameter.Name.Trim()))
                .ToArray(),
            _queryServiceSuggestion,
            CreateQueryServiceMethod,
            GetMethodName(),
            GenerateHandlerBody,
            GenerateQueryServiceBody,
            UpdateWebImports);
    }

    private QueryDtoSelectionState? CreateDtoSelectionState()
    {
        if (HasCustomDto && CustomDto is not null)
        {
            var dtoName = GetFullDtoName();
            if (string.IsNullOrWhiteSpace(dtoName))
            {
                return null;
            }

            return new QueryDtoSelectionState(
                dtoName,
                null,
                null,
                DtoLocationKind.LocalQueryDto,
                GetQueryBaseName(),
                CreateNewLocalDto: true,
                IsSelectable: true,
                SelectionBlockedReason: null);
        }

        if (ResultTypePicker.SelectedItem is { IsCustom: true })
        {
            var dtoName = ResultTypePicker.SelectedBaseName;
            if (string.IsNullOrWhiteSpace(dtoName))
            {
                return null;
            }

            return new QueryDtoSelectionState(
                dtoName,
                null,
                null,
                DtoLocationKind.SharedFeatureDto,
                null,
                CreateNewLocalDto: false,
                IsSelectable: true,
                SelectionBlockedReason: null);
        }

        if (SelectedDtoChoice is not null)
        {
            return new QueryDtoSelectionState(
                SelectedDtoChoice.Name,
                string.IsNullOrWhiteSpace(SelectedDtoChoice.Namespace) ? null : SelectedDtoChoice.Namespace,
                string.IsNullOrWhiteSpace(SelectedDtoChoice.Path) ? null : SelectedDtoChoice.Path,
                SelectedDtoChoice.LocationKind,
                SelectedDtoChoice.OwnerQueryName,
                CreateNewLocalDto: false,
                IsSelectable: SelectedDtoChoice.IsSelectable,
                SelectionBlockedReason: SelectedDtoChoice.SelectionBlockedReason);
        }

        return null;
    }

    private string? GetFullDtoName() => CustomDto is null ? null : GetFullDtoName(CustomDto);

    private string GetMethodName()
    {
        return string.IsNullOrWhiteSpace(MethodNameCyclic.Text)
            ? string.Empty
            : MethodNameCyclic.FullText;
    }

    private static string? GetFullDtoName(NewDtoDraft draft)
    {
        var suffix = draft.SuffixIndex >= 0 && draft.SuffixIndex < DtoSuffixes.Count
            ? DtoSuffixes[draft.SuffixIndex]
            : string.Empty;
        return draft.BaseName + suffix;
    }

    private static ResponseShape GetShapeFromPrefixIndex(int index)
    {
        if (index >= 0 && index < PrefixShapeMap.Count)
        {
            return PrefixShapeMap[index].Shape;
        }

        return ResponseShape.Single;
    }

    private string GetQueryBaseName() => StringUtilities.StripSuffix(QueryName, GeneratorConstants.QuerySuffix);

    private bool HasSharedConflict(DtoInfo dto, string featurePath)
    {
        if (_projectModel is null || dto.LocationKind != DtoLocationKind.LocalQueryDto)
        {
            return false;
        }

        return _projectModel.Dtos.Any(existing =>
            existing.LocationKind == DtoLocationKind.SharedFeatureDto
            && string.Equals(existing.FeaturePath, featurePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.Name, dto.Name, StringComparison.OrdinalIgnoreCase));
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
}
