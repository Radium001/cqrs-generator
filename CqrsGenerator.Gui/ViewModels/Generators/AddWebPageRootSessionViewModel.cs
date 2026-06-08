using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Collections;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class AddWebPageRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel
{
    private readonly IEmbeddedSessionHost _embeddedSessionHost;
    private readonly IAddWebPagePlanService _webPagePlanService;
    private readonly IAddQueryPlanService _queryPlanService;
    private readonly IAddQueryScenarioOutlineBuilder _queryScenarioOutlineBuilder;
    private readonly IAddWebPageScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly IQueryServiceSuggestionService _queryServiceSuggestionService;
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingPageName;
    private bool _isSyncingRoute;
    private bool _pageNameAutoDerived = true;
    private bool _routeAutoDerived = true;
    private readonly SessionArtifactRegistry _artifactRegistry = new();
    private string? _previousFeaturePath;

    public AddWebPageRootSessionViewModel(
        GenerationActionDescriptor actionDescriptor,
        IEmbeddedSessionHost embeddedSessionHost,
        IAddWebPagePlanService webPagePlanService,
        IAddQueryPlanService queryPlanService,
        IAddQueryScenarioOutlineBuilder queryScenarioOutlineBuilder,
        IAddWebPageScenarioOutlineBuilder scenarioOutlineBuilder,
        IQueryServiceSuggestionService queryServiceSuggestionService)
    {
        _embeddedSessionHost = embeddedSessionHost;
        _webPagePlanService = webPagePlanService;
        _queryPlanService = queryPlanService;
        _queryScenarioOutlineBuilder = queryScenarioOutlineBuilder;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _queryServiceSuggestionService = queryServiceSuggestionService;
        ActionDescriptor = actionDescriptor;
        SessionId = $"root:{actionDescriptor.ActionId}";
        AvailableWebFeatures = [];

        FeaturePicker = new WrappedListPickerViewModel
        {
            AllowCustom = false,
            ItemNameSelector = item => item is FeatureItemViewModel f ? f.Name : item?.ToString() ?? string.Empty,
        };
        FeaturePicker.PropertyChanged += OnFeaturePickerChanged;

        PageNameCyclic = new CyclicInputViewModel
        {
            Prefixes = [""],
            Suffixes = ["", GeneratorConstants.PageSuffix],
            SelectedIndex = 1,
        };
        PageNameCyclic.PropertyChanged += OnPageNameCyclicChanged;

        QueryPicker = new QueryPickerViewModel();
        QueryPicker.CreateQueryCommand = new RelayCommand(OpenCreateQuery, () => SelectedFeature is not null);
        QueryPicker.Picker.SelectionChanged += () =>
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanBuildPlan));
        };

        StatusText = "Configure Add Web Page.";
    }

    public string SessionId { get; }

    public GenerationActionDescriptor ActionDescriptor { get; }

    public string DisplayName => ActionDescriptor.DisplayName;

    public string Summary => "Configure Add Web Page.";

    public bool IsRoot => true;

    public bool HasUnsavedChanges =>
        SelectedFeature is not null ||
        !string.IsNullOrWhiteSpace(PageNameCyclic.Text) ||
        !string.IsNullOrWhiteSpace(Route) ||
        !CreateImports ||
        QueryPicker.GetAllSelectedOptions().Count > 0;

    public bool CanClose => true;

    public bool CanBuildPlan =>
        SelectedFeature is not null &&
        !string.IsNullOrWhiteSpace(PageNameCyclic.FullText);

    public WrappedListPickerViewModel FeaturePicker { get; }

    public CyclicInputViewModel PageNameCyclic { get; }

    public ObservableCollection<FeatureItemViewModel> AvailableWebFeatures { get; }

    public QueryPickerViewModel QueryPicker { get; }

    [ObservableProperty]
    private FeatureItemViewModel? _selectedFeature;

    [ObservableProperty]
    private string _route = string.Empty;

    [ObservableProperty]
    private bool _createImports = true;

    [ObservableProperty]
    private string _statusText = "Configure Add Web Page.";

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => _scenarioOutlineBuilder.Build(CreateFormState());

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);
        return _webPagePlanService.BuildPlan(workspaceContext, CreateFormState());
    }

    partial void OnSelectedFeatureChanged(FeatureItemViewModel? value)
    {
        QueryPicker.CreateQueryCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isSyncingFeature)
        {
            _isSyncingFeature = true;
            FeaturePicker.SelectRawItem(value);
            _isSyncingFeature = false;
        }

        if (value is not null &&
            _previousFeaturePath is not null &&
            !string.Equals(_previousFeaturePath, value.RelativePath, StringComparison.OrdinalIgnoreCase))
        {
            _artifactRegistry.ClearFeatureArtifacts();
        }
        _previousFeaturePath = value?.RelativePath;

        if (_routeAutoDerived && value is not null)
        {
            _isSyncingRoute = true;
            Route = "/" + value.Name.ToLowerInvariant();
            _isSyncingRoute = false;
        }

        if (_pageNameAutoDerived && value is not null)
        {
            _isSyncingPageName = true;
            PageNameCyclic.Text = value.Name;
            _isSyncingPageName = false;
        }

        if (_projectModel is not null)
        {
            var appFeaturePath = GetAppFeaturePath();
            if (appFeaturePath is not null)
            {
                var queries = _projectModel.Queries
                    .Where(q => string.Equals(q.FeaturePath, appFeaturePath, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var mergedQueries = queries.ToList();
                foreach (var regQuery in _artifactRegistry.QueryInfos)
                {
                    if (!mergedQueries.Any(q => q.Name == regQuery.Name))
                    {
                        mergedQueries.Add(regQuery);
                    }
                }

                QueryPicker.SetDiscovered(mergedQueries.ToArray());
            }
            else
            {
                QueryPicker.SetDiscovered([]);
            }
        }
    }

    partial void OnRouteChanged(string value)
    {
        if (_isSyncingRoute) return;
        _routeAutoDerived = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        var previousFeaturePath = SelectedFeature?.RelativePath;
        _previousFeaturePath = null;
        _artifactRegistry.ClearFeatureArtifacts();

        AvailableWebFeatures.Clear();

        if (project is null)
        {
            StatusText = "Open a project first.";
            return;
        }

        var webFeatures = project.WebFeatures
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new FeatureItemViewModel(f.Name, f.RelativePath))
            .ToList();

        foreach (var feature in webFeatures)
        {
            AvailableWebFeatures.Add(feature);
        }

        FeaturePicker.Items = webFeatures;

        var match = webFeatures.FirstOrDefault(f =>
            string.Equals(f.RelativePath, previousFeaturePath, StringComparison.OrdinalIgnoreCase))
            ?? webFeatures.FirstOrDefault();

        _isSyncingFeature = true;
        SelectedFeature = match;
        FeaturePicker.SelectRawItem(match);
        _isSyncingFeature = false;

        _pageNameAutoDerived = true;
        _routeAutoDerived = true;
        _isSyncingPageName = true;
        PageNameCyclic.Text = match?.Name ?? string.Empty;
        _isSyncingPageName = false;

        StatusText = webFeatures.Count == 0
            ? "No web features discovered."
            : "Configure Add Web Page.";
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

    private void OnPageNameCyclicChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncingPageName) return;

        if (e.PropertyName == nameof(CyclicInputViewModel.FullText))
        {
            _pageNameAutoDerived = false;
            _isSyncingPageName = true;
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(HasUnsavedChanges));

            if (_routeAutoDerived)
            {
                _isSyncingRoute = true;
                var baseName = StringUtilities.StripSuffix(PageNameCyclic.FullText, GeneratorConstants.PageSuffix);
                Route = "/" + StringUtilities.ToKebabCase(baseName);
                _isSyncingRoute = false;
            }

            _isSyncingPageName = false;
        }
    }

    private void OpenCreateQuery()
    {
        var appFeaturePath = GetAppFeaturePath();
        if (appFeaturePath is null) return;

        var querySession = new AddQueryRootSessionViewModel(
            actionDescriptor: null,
            _embeddedSessionHost,
            _queryPlanService,
            null,
            null,
            _queryServiceSuggestionService,
            _queryScenarioOutlineBuilder,
            fixedFeaturePath: appFeaturePath,
            artifactRegistry: _artifactRegistry);

        _embeddedSessionHost.Open<AddQueryFormState>(querySession, FinishQuery);
    }

    private void FinishQuery(AddQueryFormState draft)
    {
        if (draft is null) return;

        var queryName = draft.QueryName;
        var resultTypeName = draft.DtoSelection?.DtoName ?? string.Empty;
        var shape = draft.ResponseShape;

        _artifactRegistry.PublishQuery(
            queryName,
            draft,
            new QueryInfo(queryName, draft.FeaturePath ?? string.Empty, string.Empty, string.Empty));

        QueryPicker.AddGeneratedQuery(
            queryName,
            resultTypeName,
            shape,
            onRemove: _ =>
            {
                _artifactRegistry.RemoveQuery(queryName);
                OnPropertyChanged(nameof(CanBuildPlan));
                OnPropertyChanged(nameof(HasUnsavedChanges));
            });

        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string? GetAppFeaturePath()
    {
        if (_projectModel is null || SelectedFeature is null) return null;
        return SelectedFeature.RelativePath;
    }

    private AddWebPageFormState CreateFormState()
    {
        return new AddWebPageFormState(
            SelectedFeature?.RelativePath,
            SelectedFeature?.Name,
            PageNameCyclic.FullText,
            string.IsNullOrWhiteSpace(Route) ? "/" + SelectedFeature?.Name?.ToLowerInvariant() : Route,
            CreateImports,
            QueryPicker.GetSelected(),
            _artifactRegistry.QueryDrafts.Values.ToArray());
    }
}
