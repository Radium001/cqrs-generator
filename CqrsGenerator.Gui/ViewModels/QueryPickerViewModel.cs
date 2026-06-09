using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class QueryPickerViewModel : ObservableObject
{
    private readonly Dictionary<string, QueryOption> _optionsByKey = new(StringComparer.Ordinal);

    public QueryPickerViewModel()
    {
        Picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item is QueryOption option ? option.Name : item?.ToString() ?? string.Empty,
            ItemKeySelector = GetOptionKey,
            ItemCanEditSelector = item => item is QueryOption option && option.NodeId.HasValue,
            ItemCanRemoveSelector = item => item is QueryOption option && option.NodeId.HasValue,
        };
        Picker.EditItemRequested = item =>
        {
            if (item is QueryOption option)
            {
                EditRequested?.Invoke(option);
            }
        };
        Picker.RemoveItemRequested = item =>
        {
            if (item is QueryOption option)
            {
                RemoveRequested?.Invoke(option);
            }
        };
    }

    public MultiSelectListPickerViewModel Picker { get; }

    [ObservableProperty]
    private IRelayCommand? _createQueryCommand;

    public Action<QueryOption>? EditRequested { get; set; }

    public Action<QueryOption>? RemoveRequested { get; set; }

    public void SetDiscovered(IReadOnlyList<QueryInfo> queries)
    {
        _optionsByKey.Clear();
        var options = queries.Select(q =>
        {
            var reference = new ArtifactRef(
                GeneratorNodeKind.Query,
                ArtifactOrigin.Project,
                q.Name,
                FeaturePath: q.FeaturePath,
                ProjectPath: q.Path,
                Namespace: q.Namespace,
                DisplayName: q.Name);
            var option = new QueryOption(q.Name, StripQuerySuffix(q.Name), ResponseShape.Single, IsGeneratedInSession: false, reference);
            _optionsByKey[GetOptionKey(option)] = option;
            return option;
        }).ToList();

        Picker.SetDiscovered(options);
    }

    public void SetDiscovered(IEnumerable<AvailableArtifactItem> artifacts)
    {
        _optionsByKey.Clear();
        var options = new List<QueryOption>();

        foreach (var artifact in artifacts.Where(a => a.Kind == GeneratorNodeKind.Query))
        {
            var option = new QueryOption(artifact.Name, StripQuerySuffix(artifact.Name), ResponseShape.Single, artifact.IsFromSession, artifact.Ref);
            _optionsByKey[GetOptionKey(option)] = option;
            options.Add(option);
        }

        Picker.SetDiscovered(options);
    }

    public void SetSelectedQueries(IEnumerable<ArtifactRef> references)
    {
        var selectedReferences = references.ToList();
        Picker.DeselectAllCommand.Execute(null);

        foreach (var reference in selectedReferences)
        {
            var key = GetOptionKey(new QueryOption(reference.Name, StripQuerySuffix(reference.Name), ResponseShape.Single, reference.IsFromSession, reference));
            var item = Picker.FilteredItems.Cast<WrappedListItem>()
                .FirstOrDefault(w => w.OriginalItem is QueryOption option && GetOptionKey(option) == key);
            if (item?.OriginalItem is not null)
            {
                Picker.ToggleItemCommand.Execute(item.OriginalItem);
            }
        }
    }

    public IReadOnlyList<WebPageQueryBindingState> GetSelected()
    {
        return Picker.SelectedDisplayTexts
            .Select(name =>
            {
                var option = _optionsByKey.Values.FirstOrDefault(o => o.Name == name);
                return new WebPageQueryBindingState(
                    option?.Name ?? name,
                    option?.ResultTypeName ?? string.Empty,
                    string.Empty,
                    option?.Shape ?? ResponseShape.Single,
                    HasRefresh: true);
            })
            .ToList();
    }

    public IReadOnlyList<QueryOption> GetAllSelectedOptions()
    {
        return Picker.SelectedDisplayTexts
            .Select(name =>
            {
                var option = _optionsByKey.Values.FirstOrDefault(o => o.Name == name);
                return option;
            })
            .Where(option => option is not null)
            .Select(option => option!)
            .ToList();
    }

    public IReadOnlyList<QueryOption> GetSelectedOptionsByKey()
    {
        return Picker.SelectedKeys
            .Select(key => _optionsByKey.GetValueOrDefault(key))
            .Where(option => option is not null)
            .Select(option => option!)
            .ToList();
    }

    private static string StripQuerySuffix(string queryName)
    {
        if (queryName.EndsWith("Query", StringComparison.Ordinal) && queryName.Length > 5)
            return queryName[..^5];
        return queryName;
    }

    private static string GetOptionKey(object? item)
    {
        if (item is not QueryOption option)
        {
            return item?.ToString() ?? string.Empty;
        }

        return option.NodeId.HasValue
            ? $"session:{option.NodeId.Value:D}"
            : $"name:{option.Name}";
    }
}
