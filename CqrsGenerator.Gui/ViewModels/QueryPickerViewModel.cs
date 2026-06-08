using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class QueryPickerViewModel : ObservableObject
{
    private readonly Dictionary<string, QueryOption> _optionsByName = new(StringComparer.Ordinal);

    public QueryPickerViewModel()
    {
        Picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item is QueryOption option ? option.Name : item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item is QueryOption option ? option.Name : item?.ToString() ?? string.Empty,
        };
    }

    public MultiSelectListPickerViewModel Picker { get; }

    [ObservableProperty]
    private IRelayCommand? _createQueryCommand;

    public void SetDiscovered(IReadOnlyList<QueryInfo> queries)
    {
        var options = queries.Select(q =>
        {
            var option = new QueryOption(q.Name, StripQuerySuffix(q.Name), ResponseShape.Single, IsGeneratedInSession: false);
            _optionsByName[option.Name] = option;
            return option;
        }).ToList();

        Picker.SetDiscovered(options);
    }

    public void AddGeneratedQuery(string name, string resultTypeName, ResponseShape shape, Action<object>? onEdit = null, Action<object>? onRemove = null)
    {
        var option = new QueryOption(name, resultTypeName, shape, IsGeneratedInSession: true);
        _optionsByName[option.Name] = option;

        Picker.AddRuntime(
            option,
            isSelected: true,
            canEdit: onEdit is not null,
            canRemove: onRemove is not null,
            onEdit: onEdit,
            onRemove: onRemove);
    }

    public IReadOnlyList<WebPageQueryBindingState> GetSelected()
    {
        return Picker.SelectedDisplayTexts
            .Select(name =>
            {
                _optionsByName.TryGetValue(name, out var option);
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
                _optionsByName.TryGetValue(name, out var option);
                return option;
            })
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
}
