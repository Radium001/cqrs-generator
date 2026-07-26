using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class CreateFeatureRootSessionViewModel : ObservableObject,
    IRootGeneratorSessionViewModel,
    IWorkspaceAwareGeneratorSessionViewModel,
    IGeneratorNodeEditorViewModel
{
    private readonly ICreateFeaturePlanService _planService;
    private readonly GenerationActionDescriptor? _actionDescriptor;
    private readonly bool _isStandalone;
    private FeatureGeneratorState? _sessionState;
    private ProjectModel? _projectModel;
    private bool _hasNonRootSubfolder;
    private bool _isReloadingSubfolderPicker;
    private bool _isLoadingFromState;

    public GeneratorNode? Node { get; set; }

    public CreateFeatureRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        ICreateFeaturePlanService planService,
        bool isStandalone,
        GeneratorNode? node = null)
    {
        _planService = planService;
        _actionDescriptor = actionDescriptor;
        _isStandalone = isStandalone;
        Node = node;
        _sessionState = node?.State as FeatureGeneratorState;

        SessionId = isStandalone
            ? $"root:create-feature:{Guid.NewGuid():N}"
            : $"child:create-feature:{Guid.NewGuid():N}";

        SubfolderPicker = new WrappedListPickerViewModel();
        SubfolderPickerConfiguration.Configure(SubfolderPicker);
        SubfolderPicker.Items = new List<string> { "(root folder)" };
        SubfolderPicker.PropertyChanged += OnSubfolderPickerChanged;

        if (_sessionState is not null)
        {
            LoadFromSessionState(_sessionState);
        }

        StatusText = _isStandalone ? "Configure New Feature." : "Define feature draft.";
    }

    public WrappedListPickerViewModel SubfolderPicker { get; }

    public string SessionId { get; }

    public GenerationActionDescriptor? ActionDescriptor => _actionDescriptor;

    GenerationActionDescriptor IRootGeneratorSessionViewModel.ActionDescriptor =>
        _actionDescriptor ?? throw new InvalidOperationException("No action descriptor in embedded mode.");

    public string DisplayName => "New Feature";

    public string Summary => _isStandalone
        ? "Create a new feature with Queries, Commands, Interfaces folders."
        : "Define draft feature and return to parent generator.";

    public bool IsRoot => _isStandalone;

    public bool HasUnsavedChanges =>
        !string.IsNullOrWhiteSpace(FeaturePath) ||
        _hasNonRootSubfolder ||
        !CreateWebFeature;

    public bool CanClose => true;

    public bool CanBuildPlan =>
        _isStandalone &&
        !string.IsNullOrWhiteSpace(FeaturePath);

    public bool CanComplete =>
        !_isStandalone &&
        !string.IsNullOrWhiteSpace(FeaturePath);

    [ObservableProperty]
    private string _featurePath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Enter a feature name.";

    [ObservableProperty]
    private bool _createWebFeature = true;


    public void SetGenerationSession(GenerationSession session)
    {
        if (Node?.State is FeatureGeneratorState state)
        {
            _sessionState = state;
            LoadFromSessionState(state);
            SyncToSessionState();
        }
    }

    partial void OnFeaturePathChanged(string value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnCreateWebFeatureChanged(bool value)
    {
        SyncToSessionState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void SyncToSessionState()
    {
        if (_sessionState is null || _isLoadingFromState) return;
        _sessionState.FeatureName = FeaturePath?.Trim() ?? string.Empty;
        _sessionState.CreateWebFeature = CreateWebFeature;
        _sessionState.Subfolder = GetSelectedSubfolder();
    }

    private void LoadFromSessionState(FeatureGeneratorState state)
    {
        _isLoadingFromState = true;
        try
        {
            FeaturePath = state.FeatureName;
            CreateWebFeature = state.CreateWebFeature;
            RestoreSubfolderSelection(state.Subfolder);
            UpdateSubfolderFlag();
        }
        finally
        {
            _isLoadingFromState = false;
        }
    }

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        if (Node is null) return [];
        return ScenarioOutlineProjector.Project(Node);
    }

    private string? GetSelectedSubfolder()
    {
        var item = SubfolderPicker.SelectedItem;
        string? subfolder;
        if (item?.IsCustom == true)
            subfolder = SubfolderPicker.SearchText;
        else
            subfolder = SubfolderPicker.SelectedRawItem as string;

        return string.IsNullOrWhiteSpace(subfolder) ||
               string.Equals(subfolder, "(root folder)", StringComparison.Ordinal)
            ? null
            : subfolder;
    }

    private void UpdateSubfolderFlag()
    {
        var subfolder = GetSelectedSubfolder();
        _hasNonRootSubfolder = !string.IsNullOrWhiteSpace(subfolder);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void ReloadProject(ProjectModel? project)
    {
        _projectModel = project;
        _hasNonRootSubfolder = false;
        var savedSubfolder = _sessionState?.Subfolder;

        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (project is null)
        {
            _isReloadingSubfolderPicker = true;
            SubfolderPicker.Items = new List<string> { "(root folder)" };
            _isReloadingSubfolderPicker = false;
            if (SubfolderPicker.Items.Count > 0)
            {
                SubfolderPicker.SelectRawItem(SubfolderPicker.Items[0]);
            }
            StatusText = "Open a project first.";
            return;
        }

        _isReloadingSubfolderPicker = true;
        PopulateSubfolderPicker(project);
        RestoreSubfolderSelection(savedSubfolder);
        _isReloadingSubfolderPicker = false;
        SyncToSessionState();
        UpdateSubfolderFlag();
        StatusText = "Enter a feature name.";
    }

    private void PopulateSubfolderPicker(ProjectModel project)
    {
        var items = new List<string> { "(root folder)" };
        var parents = project.Features
            .Select(f => ExtractParent(f.RelativePath))
            .Where(p => p is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        items.AddRange(parents!);
        SubfolderPicker.Items = items;
    }

    private static string? ExtractParent(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;
        var slash = relativePath.LastIndexOf('/');
        return slash < 0 ? null : relativePath[..slash];
    }

    private void OnSubfolderPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WrappedListPickerViewModel.SelectedItem)
            or nameof(WrappedListPickerViewModel.SearchText))
        {
            if (_isReloadingSubfolderPicker)
            {
                return;
            }

            SyncToSessionState();
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(CanComplete));
            UpdateSubfolderFlag();
        }
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
}
