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
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class AddWebPageRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel,
    IGeneratorNodeEditorViewModel
{
    private readonly IAddWebPagePlanService _webPagePlanService;
    private ProjectModel? _projectModel;
    private bool _isSyncingFeature;
    private bool _isSyncingPageName;
    private bool _isSyncingRoute;
    private bool _pageNameAutoDerived = true;
    private bool _routeAutoDerived = true;
    private readonly WebPageGeneratorState? _sessionState;
    private GenerationSession? _generationSession;
    private IGenerationSessionNavigator? _navigator;

    private GeneratorNode? _node;
    public GeneratorNode? Node { get => _node; set => _node = value; }

    public AddWebPageRootSessionViewModel(
        GenerationActionDescriptor actionDescriptor,
        IEmbeddedSessionHost embeddedSessionHost,
        IAddWebPagePlanService webPagePlanService,
        IAddQueryPlanService queryPlanService,
        IAddQueryScenarioOutlineBuilder queryScenarioOutlineBuilder,
        IAddWebPageScenarioOutlineBuilder scenarioOutlineBuilder,
        IQueryServiceSuggestionService queryServiceSuggestionService,
        GeneratorNode? node = null)
    {
        _webPagePlanService = webPagePlanService;
        _node = node;
        _sessionState = node?.State as WebPageGeneratorState;
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
        QueryPicker.Picker.SelectionChanged += () =>
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanBuildPlan));
        };
        OpenCreateQueryCommand = new RelayCommand(OpenCreateQuery, () => Node is not null);

        if (_sessionState is not null)
        {
            PageNameCyclic.Text = _sessionState.PageName;
            Route = _sessionState.Route;
        }

        StatusText = "Configure Add Web Page.";
    }

    public void SetGenerationSession(GenerationSession session, IGenerationSessionNavigator navigator)
    {
        _generationSession = session;
        _navigator = navigator;
        if (_node is null)
        {
            var state = new WebPageGeneratorState { PageName = "NewPage" };
            _node = navigator.CreateRoot(GeneratorNodeKind.WebPage, state);
        }
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

    public IRelayCommand OpenCreateQueryCommand { get; }

    public bool ShowCreateQueryButton => Node is not null && _navigator is not null;

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

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        if (Node is null) return [];
        return ScenarioOutlineProjector.Project(Node);
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);
        return _webPagePlanService.BuildPlan(workspaceContext, CreateFormState());
    }

    partial void OnSelectedFeatureChanged(FeatureItemViewModel? value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isSyncingFeature)
        {
            _isSyncingFeature = true;
            FeaturePicker.SelectRawItem(value);
            _isSyncingFeature = false;
        }

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

                QueryPicker.SetDiscovered(queries);
            }
            else
            {
                QueryPicker.SetDiscovered(Array.Empty<QueryInfo>());
            }
        }
    }

    partial void OnRouteChanged(string value)
    {
        if (_isSyncingRoute) return;
        _routeAutoDerived = false;
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void SyncToSessionState()
    {
        if (_sessionState is null) return;
        _sessionState.PageName = PageNameCyclic.FullText;
        _sessionState.Route = Route;
        if (SelectedFeature is not null)
        {
            _sessionState.FeaturePath = SelectedFeature.RelativePath;
        }
    }

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        var previousFeaturePath = SelectedFeature?.RelativePath;

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
            SyncToSessionState();
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
            Array.Empty<AddQueryFormState>());
    }

    private void OpenCreateQuery()
    {
        if (Node is null || _navigator is null) return;
        var state = new QueryGeneratorState { QueryName = "Get" };
        var child = _navigator.CreateChild(Node, GeneratorNodeKind.Query, state);
        _navigator.OpenNode(child.Id);
    }
}