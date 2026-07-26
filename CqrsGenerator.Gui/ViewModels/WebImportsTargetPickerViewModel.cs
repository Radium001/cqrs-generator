using System.ComponentModel;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class WebImportsTargetPickerViewModel
{
    private readonly List<WebFeatureChoiceViewModel> _choices = [];
    private bool _isUpdating;

    public WebImportsTargetPickerViewModel()
    {
        Picker = new WrappedListPickerViewModel
        {
            AllowCustom = false,
            ItemNameSelector = item => item is WebFeatureChoiceViewModel choice ? choice.Name : string.Empty,
            ItemKeySelector = item => item is WebFeatureChoiceViewModel { Ref: { } reference }
                ? ArtifactKey.From(reference).Value
                : "__none__",
        };
        Picker.PropertyChanged += OnPickerPropertyChanged;
    }

    public WrappedListPickerViewModel Picker { get; }

    public ArtifactRef? SelectedRef =>
        (Picker.SelectedRawItem as WebFeatureChoiceViewModel)?.Ref;

    public event Action? SelectionChanged;

    public void Clear()
    {
        _isUpdating = true;
        try
        {
            _choices.Clear();
            _choices.Add(WebFeatureChoiceViewModel.DoNotUpdate);
            Picker.Items = _choices;
            Picker.SelectRawItem(WebFeatureChoiceViewModel.DoNotUpdate);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public void Reload(
        ProjectModel? project,
        string? applicationFeaturePath,
        ArtifactRef? preferredRef,
        bool hasExplicitSelection = false)
    {
        _isUpdating = true;
        try
        {
            _choices.Clear();
            _choices.Add(WebFeatureChoiceViewModel.DoNotUpdate);
            if (project is not null)
            {
                _choices.AddRange(project.WebFeatures
                    .OrderBy(feature => feature.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(feature => WebFeatureChoiceViewModel.FromProject(
                        feature.Name,
                        feature.RelativePath,
                        feature.Path)));
            }

            Picker.Items = _choices;
            var selected = hasExplicitSelection
                ? FindByRef(preferredRef) ?? WebFeatureChoiceViewModel.DoNotUpdate
                : FindByRef(preferredRef)
                  ?? FindByPath(applicationFeaturePath)
                  ?? WebFeatureChoiceViewModel.DoNotUpdate;
            Picker.SelectRawItem(selected);
        }
        finally
        {
            _isUpdating = false;
        }

        SelectionChanged?.Invoke();
    }

    public void SelectDefaultForFeature(string? applicationFeaturePath)
    {
        _isUpdating = true;
        try
        {
            Picker.SelectRawItem(
                FindByPath(applicationFeaturePath)
                ?? WebFeatureChoiceViewModel.DoNotUpdate);
        }
        finally
        {
            _isUpdating = false;
        }

        SelectionChanged?.Invoke();
    }

    private WebFeatureChoiceViewModel? FindByRef(ArtifactRef? reference)
    {
        if (reference is null)
        {
            return null;
        }

        return _choices.FirstOrDefault(choice =>
            choice.Ref is not null && ArtifactKey.Equals(choice.Ref, reference));
    }

    private WebFeatureChoiceViewModel? FindByPath(string? featurePath)
    {
        if (string.IsNullOrWhiteSpace(featurePath))
        {
            return null;
        }

        return _choices.FirstOrDefault(choice =>
            string.Equals(choice.RelativePath, featurePath, StringComparison.OrdinalIgnoreCase));
    }

    private void OnPickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isUpdating && e.PropertyName == nameof(WrappedListPickerViewModel.SelectedItem))
        {
            SelectionChanged?.Invoke();
        }
    }
}
