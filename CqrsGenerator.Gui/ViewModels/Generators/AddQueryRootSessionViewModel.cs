using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class AddQueryRootSessionViewModel : ObservableObject,
    IRootGeneratorSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel,
    IGeneratorNodeEditorViewModel
{
    private static readonly IReadOnlyList<string> DtoSuffixes = ["", GeneratorConstants.DtoSuffix];
    private static readonly IReadOnlyList<(string Prefix, ResponseShape Shape)> PrefixShapeMap =
        [("", ResponseShape.Single), (GeneratorConstants.ListWrapperPrefix, ResponseShape.List), (GeneratorConstants.EnumerableWrapperPrefix, ResponseShape.Enumerable)];

    private readonly IAddQueryPlanService _planService;
    private readonly IQueryServiceSuggestionService? _queryServiceSuggestionService;
    private readonly IAddQueryScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly bool _isStandalone;
    private readonly string? _fixedFeaturePath;
    private static readonly IReadOnlyList<string> MethodNameSuffixes = ["", GeneratorConstants.AsyncSuffix];
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingQueryName;
    private QueryServiceSuggestion? _queryServiceSuggestion;
    private bool _isSyncingMethodName;
    private bool _isSyncingResultTypeSelection;
    private bool _isReloadingProject;
    private bool _methodNameAutoDerived = true;
    private QueryGeneratorState? _sessionState;
    private GenerationSession? _generationSession;
    private IGenerationSessionNavigator? _navigator;

    private GeneratorNode? _node;
    public GeneratorNode? Node { get => _node; set => _node = value; }

    public AddQueryRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IAddQueryPlanService planService,
        IAddDtoPlanService? addDtoPlanService,
        IAddDtoScenarioOutlineBuilder? addDtoScenarioOutlineBuilder,
        IQueryServiceSuggestionService? queryServiceSuggestionService,
        IAddQueryScenarioOutlineBuilder scenarioOutlineBuilder,
        string? fixedFeaturePath = null,
        ICreateFeaturePlanService? createFeaturePlanService = null,
        ICreateFeatureScenarioOutlineBuilder? createFeatureScenarioOutlineBuilder = null,
        GeneratorNode? node = null)
    {
        _planService = planService;
        _queryServiceSuggestionService = queryServiceSuggestionService;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _isStandalone = actionDescriptor is not null;
        _fixedFeaturePath = fixedFeaturePath;
        _node = node;
        _sessionState = node?.State as QueryGeneratorState;
        ActionDescriptor = actionDescriptor;
        SessionId = _isStandalone
            ? $"root:{actionDescriptor!.ActionId}"
            : $"child:create-query:{Guid.NewGuid():N}";
        FeatureItems = [];
        Parameters = [];
        ResultTypeItems = [];

        FeaturePicker = new WrappedListPickerViewModel
        {
            ItemNameSelector = item => item is FeatureItemViewModel f ? f.Name : item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item is FeatureItemViewModel f && f.Ref is not null ? ArtifactKey.From(f.Ref).Value : item?.ToString() ?? string.Empty,
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
            ItemKeySelector = item => item is QueryDtoChoiceViewModel dto && dto.Ref is not null ? ArtifactKey.From(dto.Ref).Value : item?.ToString() ?? string.Empty,
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
        OpenCreateDtoCommand = new RelayCommand(OpenCreateDto, () => Node is not null && !HasOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto));
        OpenCreateFeatureCommand = new RelayCommand(OpenCreateFeature, () => Node is not null && !HasOwnedCommittedChild("Feature", GeneratorNodeKind.Feature));
        EditSelectedDtoCommand = new RelayCommand(EditSelectedDto, () => SelectedDtoChoice?.NodeId is not null);
        RemoveSelectedDtoCommand = new RelayCommand(RemoveSelectedDto, () => HasOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto));
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

        if (_sessionState is not null)
        {
            QueryNameCyclic.Text = _sessionState.QueryName;
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
        if (_node is null && _isStandalone)
        {
            var state = new QueryGeneratorState { QueryName = "Get" };
            _node = navigator.CreateRoot(GeneratorNodeKind.Query, state);
            _sessionState = state;
        }
        else if (_node?.State is QueryGeneratorState existingState)
        {
            _sessionState = existingState;
        }

        OpenCreateDtoCommand.NotifyCanExecuteChanged();
        OpenCreateFeatureCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowCreateDtoButton));
        OnPropertyChanged(nameof(ShowCreateFeatureButton));
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

    public ObservableCollection<FeatureItemViewModel> FeatureItems { get; }

    public ObservableCollection<QueryDtoChoiceViewModel> ResultTypeItems { get; }

    public ObservableCollection<PropertyEntryViewModel> Parameters { get; }

    public PropertyEntryListEditorViewModel ParameterEditor { get; }

    public QueryDtoChoiceViewModel? SelectedDtoChoice => ResultTypePicker.SelectedRawItem as QueryDtoChoiceViewModel;

    public QueryDtoSelectionState? SelectedDtoSelection => CreateDtoSelectionState();

    public bool ShowDtoPromotionHint => SelectedDtoRequiresPromotion && SelectedDtoChoice?.NodeId is null;

    public bool SelectedDtoRequiresPromotion => SelectedDtoChoice?.IsLocal == true;

    public bool SelectedDtoSelectionBlocked => SelectedDtoChoice is { IsSelectable: false };

    public string DtoPromotionHintText => SelectedDtoChoice is { IsLocal: true, OwnerQueryName: not null }
        ? $"{SelectedDtoChoice.OwnerQueryName}/{SelectedDtoChoice.Name} -> DTOs/{SelectedDtoChoice.Name}"
        : string.Empty;

    public QueryServiceItemViewModel? AutoQueryService { get; private set; }

    public IRelayCommand AddParameterCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemoveParameterCommand { get; }

    public IRelayCommand OpenCreateDtoCommand { get; }

    public IRelayCommand OpenCreateFeatureCommand { get; }

    public IRelayCommand EditSelectedDtoCommand { get; }

    public IRelayCommand RemoveSelectedDtoCommand { get; }

    public IRelayCommand EditSelectedFeatureCommand { get; }

    public IRelayCommand RemoveSelectedFeatureCommand { get; }

    public bool ShowCreateDtoButton => Node is not null && _navigator is not null && !HasOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto);

    public bool ShowCreateFeatureButton => Node is not null && _navigator is not null && !HasOwnedCommittedChild("Feature", GeneratorNodeKind.Feature);

    public bool CanEditSelectedDto => SelectedDtoChoice?.NodeId is not null;

    public bool CanRemoveSelectedDto => HasOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto);

    public bool CanEditSelectedFeature => SelectedFeature?.NodeId is not null;

    public bool CanRemoveSelectedFeature => HasOwnedCommittedChild("Feature", GeneratorNodeKind.Feature);

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

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        if (Node is null) return [];
        return ScenarioOutlineProjector.Project(Node);
    }

    partial void OnSelectedFeatureChanged(FeatureItemViewModel? value)
    {
        if (!_isSyncingFeature)
        {
            _isSyncingFeature = true;
            FeaturePicker.SelectRawItem(value);
            _isSyncingFeature = false;
        }

        if (_isReloadingProject)
        {
            return;
        }

        if (value is not null && Node is not null)
        {
            foreach (var child in Node.Children)
            {
                if (child.Lifecycle == GeneratorNodeLifecycle.Committed &&
                    child.State is DtoGeneratorState dtoState &&
                    !ArtifactKey.Equals(dtoState.FeatureRef, value.Ref))
                {
                    dtoState.FeatureRef = value.Ref;
                }
            }
        }

        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanEditSelectedFeature));
        OnPropertyChanged(nameof(CanRemoveSelectedFeature));
        EditSelectedFeatureCommand.NotifyCanExecuteChanged();
        RemoveSelectedFeatureCommand.NotifyCanExecuteChanged();

        if (_projectModel is null || value is null)
        {
            ResultTypeItems.Clear();
            ResultTypePicker.RawItems = ResultTypeItems;
            SetQueryServiceSuggestion(null);
            OnPropertyChanged(nameof(SelectedDtoChoice));
            OnPropertyChanged(nameof(CanEditSelectedDto));
            OnPropertyChanged(nameof(CanRemoveSelectedDto));
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
        if (_isReloadingProject)
        {
            return;
        }

        SyncToSessionState();
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

        if (_queryServiceSuggestionService is not null && _projectModel is not null && SelectedFeature is not null)
        {
            var suggestion = _queryServiceSuggestionService.Suggest(_projectModel, SelectedFeature.Name, SelectedFeature.RelativePath);
            SetQueryServiceSuggestion(suggestion);
        }

        RefreshDtoChoices();

        if (_sessionState?.ResultDtoRef?.IsFromSession == true)
        {
            LockCommittedDto();
            ResultTypePicker.RefreshFilteredItems();
        }
    }

    private void ReloadProject(ProjectModel? project)
    {
        _isReloadingProject = true;
        try
        {
        _projectModel = project;
        _methodNameAutoDerived = true;
        _isSyncingMethodName = true;
        MethodNameCyclic.Text = QueryNameCyclic.FullText;
        _isSyncingMethodName = false;
        var previousFeatureRef = _sessionState?.FeatureRef;

        FeatureItems.Clear();
        FeaturePicker.UnlockSelection();
        ResultTypeItems.Clear();
        ResultTypePicker.UnlockSelection();
        SetQueryServiceSuggestion(null);

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
            FeatureItems.Add(feature);
        }
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
                previousFeatureRef is not null &&
                feature.Ref is not null &&
                ArtifactRefEquals(feature.Ref, previousFeatureRef))
                ?? features.FirstOrDefault(feature =>
                string.Equals(feature.RelativePath, SelectedFeature?.RelativePath, StringComparison.OrdinalIgnoreCase))
                ?? features.FirstOrDefault();

            _isSyncingFeature = true;
            SelectedFeature = match;
            FeaturePicker.SelectRawItem(match);
            _isSyncingFeature = false;
        }

        LockCommittedFeature();

        _isSyncingQueryName = true;
        QueryName = QueryNameCyclic.FullText;
        _isSyncingQueryName = false;

        StatusText = features.Count == 0
            ? "No features discovered."
            : "Configure Add Query.";

        }
        finally
        {
            _isReloadingProject = false;
        }

        if (_projectModel is not null && SelectedFeature is not null)
        {
            RefreshDtoChoices();

            if (_queryServiceSuggestionService is not null)
            {
                var suggestion = _queryServiceSuggestionService.Suggest(_projectModel, SelectedFeature.Name, SelectedFeature.RelativePath);
                SetQueryServiceSuggestion(suggestion);
            }
        }

        RestoreDtoSelection();
        LockCommittedDto();
        OnPropertyChanged(nameof(SelectedDtoChoice));
        OnPropertyChanged(nameof(CanEditSelectedDto));
        OnPropertyChanged(nameof(CanRemoveSelectedDto));
        EditSelectedDtoCommand.NotifyCanExecuteChanged();
        RemoveSelectedDtoCommand.NotifyCanExecuteChanged();
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanEditSelectedFeature));
        OnPropertyChanged(nameof(CanRemoveSelectedFeature));
        EditSelectedFeatureCommand.NotifyCanExecuteChanged();
        RemoveSelectedFeatureCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowCreateDtoButton));
        OnPropertyChanged(nameof(ShowCreateFeatureButton));
        OpenCreateDtoCommand.NotifyCanExecuteChanged();
        OpenCreateFeatureCommand.NotifyCanExecuteChanged();
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
            if (!_isSyncingMethodName)
            {
                _methodNameAutoDerived = false;
            }

            SyncToSessionState();
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    partial void OnCreateQueryServiceMethodChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnGenerateHandlerBodyChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnGenerateQueryServiceBodyChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnUpdateWebImportsChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void OnResultTypePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isSyncingResultTypeSelection)
        {
            return;
        }

        if (e.PropertyName is nameof(WrappedListPickerViewModel.SelectedPrefixIndex)
            or nameof(WrappedListPickerViewModel.SelectedItem)
            or nameof(WrappedListPickerViewModel.SearchText)
            or nameof(WrappedListPickerViewModel.SelectedBaseName))
        {
            SyncToSessionState();
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(SelectedDtoChoice));
            OnPropertyChanged(nameof(SelectedDtoSelection));
            OnPropertyChanged(nameof(ShowDtoPromotionHint));
            OnPropertyChanged(nameof(DtoPromotionHintText));
            OnPropertyChanged(nameof(SelectedDtoRequiresPromotion));
            OnPropertyChanged(nameof(SelectedDtoSelectionBlocked));
            EditSelectedDtoCommand.NotifyCanExecuteChanged();
            RemoveSelectedDtoCommand.NotifyCanExecuteChanged();
        }
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
            ResultTypeItems.Clear();
            ResultTypePicker.RawItems = ResultTypeItems;
            return;
        }

        var featurePath = SelectedFeature.RelativePath;
        var queryBaseName = GetQueryBaseName();
        var discoveredDtos = BuildDtoChoices(featurePath, queryBaseName);
        var selectedDtoRef = SelectedDtoChoice?.Ref ?? _sessionState?.ResultDtoRef;

        _isSyncingResultTypeSelection = true;
        try
        {
            ResultTypeItems.Clear();
            foreach (var dto in discoveredDtos)
            {
                ResultTypeItems.Add(dto);
            }

            ResultTypePicker.RawItems = ResultTypeItems;
            if (selectedDtoRef is not null && !SelectDto(selectedDtoRef))
            {
                ResultTypePicker.SelectRawItem(null);
                if (_sessionState is not null && _sessionState.ResultDtoRef is not null && !HasOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto))
                {
                    _sessionState.ResultDtoRef = null;
                }
            }
        }
        finally
        {
            _isSyncingResultTypeSelection = false;
        }

        OnPropertyChanged(nameof(SelectedDtoChoice));
        OnPropertyChanged(nameof(SelectedDtoSelection));
        OnPropertyChanged(nameof(ShowDtoPromotionHint));
        OnPropertyChanged(nameof(DtoPromotionHintText));
        OnPropertyChanged(nameof(SelectedDtoRequiresPromotion));
        OnPropertyChanged(nameof(SelectedDtoSelectionBlocked));
        EditSelectedDtoCommand.NotifyCanExecuteChanged();
        RemoveSelectedDtoCommand.NotifyCanExecuteChanged();
    }

    private QueryDtoSelectionState? CreateDtoSelectionState()
    {
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
            var isOwnedResultDto = SelectedDtoChoice.NodeId is Guid dtoNodeId &&
                FindOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto)?.Id == dtoNodeId;

            return new QueryDtoSelectionState(
                SelectedDtoChoice.Name,
                isOwnedResultDto ? null : string.IsNullOrWhiteSpace(SelectedDtoChoice.Namespace) ? null : SelectedDtoChoice.Namespace,
                isOwnedResultDto ? null : string.IsNullOrWhiteSpace(SelectedDtoChoice.Path) ? null : SelectedDtoChoice.Path,
                isOwnedResultDto ? DtoLocationKind.LocalQueryDto : SelectedDtoChoice.LocationKind,
                isOwnedResultDto ? StringUtilities.StripSuffix(QueryName, GeneratorConstants.QuerySuffix) : SelectedDtoChoice.OwnerQueryName,
                CreateNewLocalDto: isOwnedResultDto,
                IsSelectable: SelectedDtoChoice.IsSelectable,
                SelectionBlockedReason: SelectedDtoChoice.SelectionBlockedReason);
        }

        return null;
    }

    private string GetMethodName()
    {
        return string.IsNullOrWhiteSpace(MethodNameCyclic.Text)
            ? string.Empty
            : MethodNameCyclic.FullText;
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

    private void SyncToSessionState()
    {
        if (_sessionState is null) return;
        _sessionState.QueryName = QueryNameCyclic.FullText.Trim();
        _sessionState.FeatureRef = SelectedFeature?.Ref;
        _sessionState.ResultDtoRef = SelectedDtoChoice?.Ref;
        _sessionState.CustomDtoName = ResultTypePicker.SelectedItem?.IsCustom == true ? ResultTypePicker.SelectedBaseName : null;
        _sessionState.CreateQueryServiceMethod = CreateQueryServiceMethod;
        _sessionState.MethodName = GetMethodName();
        _sessionState.GenerateHandlerBody = GenerateHandlerBody;
        _sessionState.GenerateQueryServiceBody = GenerateQueryServiceBody;
        _sessionState.UpdateWebImports = UpdateWebImports;
        _sessionState.ResponseShape = GetShapeFromPrefixIndex(ResultTypePicker.SelectedPrefixIndex);
        _sessionState.Parameters.Clear();
        foreach (var p in Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Type) && !string.IsNullOrWhiteSpace(p.Name)))
        {
            _sessionState.Parameters.Add(new PropertySpec(p.Type.Trim(), p.Name.Trim()));
        }
    }

    private void LockCommittedFeature()
    {
        var committedFeature = FindOwnedCommittedChild("Feature", GeneratorNodeKind.Feature);
        if (committedFeature is null)
        {
            return;
        }

        var match = FeatureItems.FirstOrDefault(f => f.NodeId == committedFeature.Id);
        if (match is not null)
        {
            _isSyncingFeature = true;
            SelectedFeature = match;
            FeaturePicker.LockSelection(match);
            _isSyncingFeature = false;
        }
    }

    private void LockCommittedDto()
    {
        var committedDto = Node?.Children.FirstOrDefault(c =>
            c.Lifecycle == GeneratorNodeLifecycle.Committed &&
            string.Equals(c.RelationshipName, "ResultDto", StringComparison.Ordinal));
        if (committedDto is not null)
        {
            var match = ResultTypeItems.FirstOrDefault(dto => dto.NodeId == committedDto.Id);
            if (match is not null)
            {
                ResultTypePicker.LockSelection(match);
            }
        }
    }

    private void OpenCreateDto()
    {
        if (Node is null || _navigator is null || _generationSession is null) return;
        var featureRef = _sessionState?.FeatureRef ?? SelectedFeature?.Ref;
        var state = new DtoGeneratorState
        {
            BaseName = "NewDto",
            FeatureRef = featureRef,
        };
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Dto, state, "ResultDto");
        _navigator.OpenNode(child.Id);
    }

    private void OpenCreateFeature()
    {
        if (Node is null || _navigator is null || _generationSession is null) return;
        var state = new FeatureGeneratorState { FeatureName = "NewFeature", CreateWebFeature = true };
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Feature, state, "Feature");
        _navigator.OpenNode(child.Id);
    }

    private void EditSelectedDto()
    {
        if (SelectedDtoChoice?.NodeId is Guid nodeId)
        {
            _navigator?.OpenNode(nodeId);
        }
    }

    private void RemoveSelectedDto()
    {
        if (Node is null || _navigator is null)
        {
            return;
        }

        var child = FindOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto);
        if (child is null)
        {
            return;
        }

        if (_navigator.RemoveNode(child.Id))
        {
            if (_sessionState is not null)
            {
                _sessionState.ResultDtoRef = null;
                _sessionState.CustomDtoName = null;
            }

            ResultTypePicker.UnlockSelection(clearSearchText: true);
            RefreshDtoChoices();
            ResultTypePicker.SelectRawItem(null);
            NotifySelectionAndCommands();
        }
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
        if (Node is null || _navigator is null)
        {
            return;
        }

        var child = FindOwnedCommittedChild("Feature", GeneratorNodeKind.Feature);
        if (child is null)
        {
            return;
        }

        if (FindOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto)?.State is DtoGeneratorState dtoState &&
            dtoState.FeatureRef?.NodeId == child.Id)
        {
            dtoState.FeatureRef = null;
        }

        if (_navigator.RemoveNode(child.Id))
        {
            if (_sessionState is not null && _sessionState.FeatureRef?.NodeId == child.Id)
            {
                _sessionState.FeatureRef = null;
            }

            FeaturePicker.UnlockSelection(clearSearchText: true);
            ReloadProject(_projectModel);
            if (_sessionState?.FeatureRef is not null)
            {
                PropagateFeatureToOwnedDto(_sessionState.FeatureRef);
            }
            NotifySelectionAndCommands();
        }
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

    private void PropagateFeatureToOwnedDto(ArtifactRef? featureRef)
    {
        var ownedDto = FindOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto);
        if (ownedDto?.State is DtoGeneratorState dtoState)
        {
            dtoState.FeatureRef = featureRef;
        }
    }

    private void NotifySelectionAndCommands()
    {
        OnPropertyChanged(nameof(ShowCreateDtoButton));
        OnPropertyChanged(nameof(ShowCreateFeatureButton));
        OnPropertyChanged(nameof(CanEditSelectedDto));
        OnPropertyChanged(nameof(CanRemoveSelectedDto));
        OnPropertyChanged(nameof(CanEditSelectedFeature));
        OnPropertyChanged(nameof(CanRemoveSelectedFeature));
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OpenCreateDtoCommand.NotifyCanExecuteChanged();
        OpenCreateFeatureCommand.NotifyCanExecuteChanged();
        EditSelectedDtoCommand.NotifyCanExecuteChanged();
        RemoveSelectedDtoCommand.NotifyCanExecuteChanged();
        EditSelectedFeatureCommand.NotifyCanExecuteChanged();
        RemoveSelectedFeatureCommand.NotifyCanExecuteChanged();
    }

    private List<QueryDtoChoiceViewModel> BuildDtoChoices(string featurePath, string queryBaseName)
    {
        var discoveredDtos = new List<QueryDtoChoiceViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var dto in _projectModel.Dtos
                .Where(dto => string.Equals(dto.FeaturePath, featurePath, StringComparison.OrdinalIgnoreCase))
                .Where(dto => dto.LocationKind == DtoLocationKind.SharedFeatureDto
                    || !string.Equals(dto.OwnerQueryName, queryBaseName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(dto => dto.LocationKind == DtoLocationKind.SharedFeatureDto ? 0 : 1)
                .ThenBy(dto => dto.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var reference = new ArtifactRef(
                    GeneratorNodeKind.Dto,
                    ArtifactOrigin.Project,
                    dto.Name,
                    FeaturePath: dto.FeaturePath,
                    ProjectPath: dto.Path,
                    Namespace: dto.Namespace,
                    OwnerName: dto.OwnerQueryName,
                    DisplayName: dto.DisplayName);
                var blocked = dto.LocationKind == DtoLocationKind.LocalQueryDto && HasSharedConflict(dto, featurePath);
                if (seen.Add(GetDtoKey(reference)))
                {
                    discoveredDtos.Add(new QueryDtoChoiceViewModel(
                        reference,
                        dto.DisplayName,
                        dto.Namespace,
                        dto.LocationKind,
                        dto.OwnerQueryName,
                        dto.Path,
                        isSelectable: !blocked,
                        selectionBlockedReason: blocked ? "A shared DTO with the same name already exists." : null));
                }
            }
        }

        if (_generationSession is not null)
        {
            foreach (var sessionDto in _generationSession.Artifacts.GetDtos(featurePath).OrderBy(dto => dto.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Add(GetDtoKey(sessionDto.Ref)))
                {
                    var isOwnedResultDto = sessionDto.NodeId is Guid nodeId &&
                        FindOwnedCommittedChild("ResultDto", GeneratorNodeKind.Dto)?.Id == nodeId;
                    var isLocalQueryDto = isOwnedResultDto || !string.IsNullOrWhiteSpace(sessionDto.Ref.OwnerName);
                    discoveredDtos.Add(new QueryDtoChoiceViewModel(
                        sessionDto.Ref,
                        sessionDto.DisplayName,
                        sessionDto.Ref.Namespace ?? string.Empty,
                        isLocalQueryDto ? DtoLocationKind.LocalQueryDto : DtoLocationKind.SharedFeatureDto,
                        isOwnedResultDto ? queryBaseName : sessionDto.Ref.OwnerName,
                        sessionDto.Ref.ProjectPath,
                        isSelectable: sessionDto.IsSelectable && (isOwnedResultDto || !isLocalQueryDto),
                        selectionBlockedReason: !isOwnedResultDto && isLocalQueryDto
                            ? "Session-local query DTOs can be used after they are generated."
                            : sessionDto.SelectionBlockedReason));
                }
            }
        }

        return discoveredDtos;
    }

    private void RestoreDtoSelection()
    {
        var dtoRef = _sessionState?.ResultDtoRef;
        if (dtoRef is not null)
        {
            SelectDto(dtoRef);
        }
    }

    private bool SelectDto(ArtifactRef? reference)
    {
        if (reference is null)
        {
            return false;
        }

        var match = ResultTypeItems.FirstOrDefault(dto => dto.Ref is not null && ArtifactRefEquals(dto.Ref, reference));
        if (match is not null)
        {
            ResultTypePicker.SelectRawItem(match);
            return true;
        }

        return false;
    }

    private void SelectFeature(ArtifactRef? reference)
    {
        if (reference is null)
        {
            return;
        }

        var match = FeatureItems.FirstOrDefault(feature => feature.Ref is not null && ArtifactRefEquals(feature.Ref, reference));
        if (match is not null)
        {
            _isSyncingFeature = true;
            SelectedFeature = match;
            FeaturePicker.SelectRawItem(match);
            _isSyncingFeature = false;
        }
    }

    private static bool ArtifactRefEquals(ArtifactRef left, ArtifactRef right)
    {
        return ArtifactKey.Equals(left, right);
    }

    private static string GetDtoKey(ArtifactRef reference)
    {
        return ArtifactKey.From(reference).Value;
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
                var key = feature.Ref.NodeId.HasValue
                    ? $"session:{feature.Ref.NodeId.Value:D}"
                    : feature.FeaturePath ?? feature.Name;
                if (seen.Add(key))
                {
                    items.Add(new FeatureItemViewModel(feature.Ref));
                }
            }
        }

        return items;
    }
}
