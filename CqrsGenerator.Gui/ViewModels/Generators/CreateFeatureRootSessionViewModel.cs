using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public sealed partial class CreateFeatureRootSessionViewModel : ObservableObject,
    IPlanBuildingRootSessionViewModel,
    IEmbeddedGeneratorSessionViewModel<NewFeatureDraft>,
    IWorkspaceAwareGeneratorSessionViewModel
{
    private readonly ICreateFeaturePlanService _planService;
    private readonly ICreateFeatureScenarioOutlineBuilder _scenarioOutlineBuilder;
    private readonly GenerationActionDescriptor? _actionDescriptor;
    private readonly bool _isStandalone;
    private readonly NewFeatureDraft? _existingDraft;
    private ProjectModel? _projectModel;
    private bool _hasNonRootSubfolder;

    public CreateFeatureRootSessionViewModel(
        GenerationActionDescriptor? actionDescriptor,
        ICreateFeaturePlanService planService,
        ICreateFeatureScenarioOutlineBuilder scenarioOutlineBuilder,
        bool isStandalone,
        NewFeatureDraft? existingDraft = null)
    {
        _planService = planService;
        _scenarioOutlineBuilder = scenarioOutlineBuilder;
        _actionDescriptor = actionDescriptor;
        _isStandalone = isStandalone;
        _existingDraft = existingDraft;

        SessionId = isStandalone
            ? $"root:create-feature:{Guid.NewGuid():N}"
            : $"child:create-feature:{Guid.NewGuid():N}";

        SubfolderPicker = new WrappedListPickerViewModel();
        SubfolderPickerConfiguration.Configure(SubfolderPicker);
        SubfolderPicker.Items = new List<string> { "(root folder)" };
        SubfolderPicker.PropertyChanged += OnSubfolderPickerChanged;

        if (existingDraft is not null)
        {
            FeaturePath = existingDraft.FeatureName;
            CreateWebFeature = existingDraft.CreateWebFeature;
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
        _hasNonRootSubfolder;

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

    partial void OnFeaturePathChanged(string value)
    {
        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
    {
        ReloadProject(workspaceContext?.ProjectModel);
    }

    public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes()
    {
        var formState = new CreateFeatureFormState(
            FeaturePath?.Trim(),
            GetSelectedSubfolder(),
            CreateWebFeature);
        return _scenarioOutlineBuilder.Build(formState);
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext)
    {
        ArgumentNullException.ThrowIfNull(workspaceContext);

        var formState = new CreateFeatureFormState(
            FeaturePath?.Trim(),
            GetSelectedSubfolder(),
            CreateWebFeature);

        return _planService.BuildPlan(workspaceContext, formState);
    }

    public NewFeatureDraft BuildDraft()
    {
        return new NewFeatureDraft(
            FeaturePath?.Trim() ?? string.Empty,
            GetSelectedSubfolder(),
            CreateWebFeature);
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

        OnPropertyChanged(nameof(CanBuildPlan));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (project is null)
        {
            SubfolderPicker.Items = new List<string> { "(root folder)" };
            StatusText = "Open a project first.";
            return;
        }

        PopulateSubfolderPicker(project);
        RestoreSubfolderFromDraft();
        StatusText = "Enter a feature name.";
    }

    private void RestoreSubfolderFromDraft()
    {
        var subfolder = _existingDraft?.Subfolder;
        if (string.IsNullOrWhiteSpace(subfolder))
            return;

        var item = SubfolderPicker.Items?
            .OfType<string>()
            .FirstOrDefault(s => string.Equals(s, subfolder, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
            SubfolderPicker.SelectRawItem(item);
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
            OnPropertyChanged(nameof(CanBuildPlan));
            OnPropertyChanged(nameof(CanComplete));
            UpdateSubfolderFlag();
        }
    }
}
