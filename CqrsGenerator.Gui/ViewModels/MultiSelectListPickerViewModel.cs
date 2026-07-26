using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class MultiSelectListPickerViewModel : ObservableObject
{
    private readonly List<object> _discoveredItems = [];
    private readonly HashSet<string> _selectedKeys = [];
    private readonly ObservableCollection<WrappedListItem> _allWrapped = [];

    public MultiSelectListPickerViewModel()
    {
        FilteredItems = _allWrapped;
    }

    public Func<object?, string>? ItemNameSelector { get; set; }

    public Func<object?, string>? ItemKeySelector { get; set; }

    public Func<object?, bool>? ItemCanEditSelector { get; set; }

    public Func<object?, bool>? ItemCanRemoveSelector { get; set; }

    public Action<object>? EditItemRequested { get; set; }

    public Action<object>? RemoveItemRequested { get; set; }

    public IEnumerable FilteredItems { get; }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddItem))]
    private IRelayCommand? _addItemCommand;

    public string SearchWatermark => "Search...";

    public bool HasAddItem => AddItemCommand is not null;

    public IReadOnlyCollection<string> SelectedKeys => _selectedKeys;

    public IReadOnlyList<string> SelectedDisplayTexts =>
        _allWrapped.Where(w => w.IsSelected).Select(w => w.DisplayText).ToList();

    public event Action? SelectionChanged;

    public void SetDiscovered(IEnumerable items)
    {
        _discoveredItems.Clear();
        foreach (var item in items)
        {
            _discoveredItems.Add(item);
        }

        PruneSelections();
        RebuildWrappedItems();
    }

    public void SetDiscovered(IEnumerable<AvailableArtifactItem> items)
    {
        if (ItemNameSelector is null)
            ItemNameSelector = item => item is AvailableArtifactItem a ? a.Name : item?.ToString() ?? string.Empty;
        if (ItemKeySelector is null)
            ItemKeySelector = item => item is AvailableArtifactItem a ? a.Name : item?.ToString() ?? string.Empty;

        SetDiscovered((IEnumerable)items);
    }

    [RelayCommand]
    private void ToggleItem(object? originalItem)
    {
        if (originalItem is null) return;

        var key = GetItemKey(originalItem);
        if (!_selectedKeys.Remove(key))
        {
            _selectedKeys.Add(key);
        }

        RebuildWrappedItems();
        SelectionChanged?.Invoke();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in _allWrapped)
        {
            if (item.OriginalItem is not null)
            {
                _selectedKeys.Add(GetItemKey(item.OriginalItem));
            }
        }

        RebuildWrappedItems();
        SelectionChanged?.Invoke();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        _selectedKeys.Clear();
        RebuildWrappedItems();
        SelectionChanged?.Invoke();
    }

    [RelayCommand]
    private void RequestAddItem()
    {
        AddItemCommand?.Execute(null);
    }

    [RelayCommand]
    private void RequestRemoveItem(object? originalItem)
    {
        if (originalItem is null) return;

        RemoveItemRequested?.Invoke(originalItem);
    }

    [RelayCommand]
    private void RequestEditItem(object? originalItem)
    {
        if (originalItem is null) return;

        EditItemRequested?.Invoke(originalItem);
    }

    public void SelectNext()
    {
        if (_allWrapped.Count == 0) return;
        var firstUnselected = _allWrapped.FirstOrDefault(w => !w.IsSelected);
        if (firstUnselected?.OriginalItem is null) return;

        var key = GetItemKey(firstUnselected.OriginalItem);
        if (!_selectedKeys.Remove(key))
        {
            _selectedKeys.Add(key);
        }

        RebuildWrappedItems();
    }

    public void SelectPrevious()
    {
        if (_allWrapped.Count == 0) return;
        var lastSelected = _allWrapped.LastOrDefault(w => w.IsSelected);
        if (lastSelected?.OriginalItem is null) return;

        _selectedKeys.Remove(GetItemKey(lastSelected.OriginalItem));
        RebuildWrappedItems();
    }

    partial void OnSearchTextChanged(string value)
    {
        RebuildWrappedItems();
    }

    private void RebuildWrappedItems()
    {
        _allWrapped.Clear();
        var search = (SearchText ?? "").Trim();

        foreach (var item in _discoveredItems)
        {
            var name = ItemNameSelector?.Invoke(item) ?? item?.ToString() ?? "";
            if (ShouldFilter(name, search))
                continue;

            _allWrapped.Add(new WrappedListItem(item, name, false, name)
            {
                IsSelected = _selectedKeys.Contains(GetItemKey(item)),
                CanEdit = ItemCanEditSelector?.Invoke(item) ?? false,
                CanRemove = ItemCanRemoveSelector?.Invoke(item) ?? false,
            });
        }

        OnPropertyChanged(nameof(FilteredItems));
    }

    private static bool ShouldFilter(string name, string search)
    {
        return !string.IsNullOrWhiteSpace(search) &&
               !name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private string GetItemKey(object? item)
    {
        return ItemKeySelector?.Invoke(item)
            ?? ItemNameSelector?.Invoke(item)
            ?? item?.ToString()
            ?? string.Empty;
    }

    private void PruneSelections()
    {
        var knownKeys = _discoveredItems.Select(GetItemKey)
            .ToHashSet(StringComparer.Ordinal);

        _selectedKeys.RemoveWhere(key => !knownKeys.Contains(key));
    }
}
