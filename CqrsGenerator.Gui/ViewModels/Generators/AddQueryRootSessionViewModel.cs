using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Collections;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class AddQueryRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
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
    private bool _methodNameAutoDerived = true;
    private readonly QueryGeneratorState? _sessionState;
    private GenerationSession? _generationSession;
    private IGenerationSessionNavigator? _navigator;

    private GeneratorNode? _node;
    public GeneratorNode? Node { get => _node; set => _node = value; }

    public AddQueryRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        IEmbeddedSessionHost embeddedSessionHost,
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
        OpenCreateDtoCommand = new RelayCommand(OpenCreateDto, () => Node is not null);
        OpenCreateFeatureCommand = new RelayCommand(OpenCreateFeature, () => Node is not null);
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
                Parameters.Add(new PropertyEntryViewModel(param.Type, param.Name));
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
        }
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

    public RuntimeItemCollection<FeatureItemViewModel> FeatureItems { get; }

    public RuntimeItemCollection<QueryDtoChoiceViewModel> ResultTypeItems { get; }

    public ObservableCollection<PropertyEntryViewModel> Parameters { get; }

    public PropertyEntryListEditorViewModel ParameterEditor { get; }

    public QueryDtoChoiceViewModel? SelectedDtoChoice => ResultTypePicker.SelectedRawItem as QueryDtoChoiceViewModel;

    public QueryDtoSelectionState? SelectedDtoSelection => CreateDtoSelectionState();

    public bool ShowDtoPromotionHint => SelectedDtoRequiresPromotion;

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

    public bool ShowCreateDtoButton => Node is not null && _navigator is not null;

    public bool ShowCreateFeatureButton => Node is not null && _navigator is not null;

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

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);
        return _planService.BuildPlan(workspaceContext, CreateFormState());
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
        SetQueryServiceSuggestion(null);

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
        SyncToSessionState();
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
            SyncToSessionState();
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

        if (_generationSession is not null)
        {
            var sessionDtos = _generationSession.Artifacts.GetDtos(featurePath);
            foreach (var sessionDto in sessionDtos)
            {
                if (!discoveredDtos.Any(d => d.Name == sessionDto.Name))
                {
                    discoveredDtos.Add(new QueryDtoChoiceViewModel(
                        sessionDto.Name,
                        sessionDto.Name + " (session)",
                        string.Empty,
                        DtoLocationKind.LocalQueryDto,
                        queryBaseName,
                        string.Empty,
                        isRuntime: true));
                }
            }
        }

        ResultTypeItems.SetDiscovered(discoveredDtos);
        ResultTypePicker.RawItems = ResultTypeItems;
    }

    private AddQueryFormState CreateFormState()
    {
        return new AddQueryFormState(
            SelectedFeature?.Name,
            SelectedFeature?.RelativePath,
            QueryName,
            CreateDtoSelectionState(),
            null,
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
        Parameters.Add(new PropertyEntryViewModel("string", $"param{Parameters.Count + 1}"));
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void RemoveParameter(PropertyEntryViewModel? parameter)
    {
        if (parameter is null)
        {
            return;
        }

        Parameters.Remove(parameter);
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void SyncToSessionState()
    {
        if (_sessionState is null) return;
        _sessionState.QueryName = QueryNameCyclic.FullText.Trim();
        if (SelectedFeature is not null)
        {
            _sessionState.FeaturePath = SelectedFeature.RelativePath;
        }
        _sessionState.ExistingResultDtoName = ResultTypePicker.SelectedBaseName;
        _sessionState.Parameters.Clear();
        foreach (var p in Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Type) && !string.IsNullOrWhiteSpace(p.Name)))
        {
            _sessionState.Parameters.Add(new PropertySpec(p.Type.Trim(), p.Name.Trim()));
        }
    }

    private void OpenCreateDto()
    {
        if (Node is null || _navigator is null) return;
        var state = new DtoGeneratorState { BaseName = "NewDto" };
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Dto, state);
        _navigator.OpenNode(child.Id);
    }

    private void OpenCreateFeature()
    {
        if (Node is null || _navigator is null) return;
        var state = new FeatureGeneratorState { FeatureName = "NewFeature" };
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Feature, state);
        _navigator.OpenNode(child.Id);
    }
}