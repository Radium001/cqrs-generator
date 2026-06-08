using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class MultiSelectListPickerViewModel : ObservableObject
{
    private readonly List<object> _discoveredItems = [];
    private readonly List<object> _runtimeItems = [];
    private readonly HashSet<string> _selectedKeys = [];
    private readonly ObservableCollection<WrappedListItem> _allWrapped = [];
    private readonly Dictionary<string, RuntimeItemOptions> _runtimeOptions = [];
    private IList _rawItems = new List<object>();
    private INotifyCollectionChanged? _rawCollectionNotifier;

    public MultiSelectListPickerViewModel()
    {
        FilteredItems = _allWrapped;
    }

    public Func<object?, string>? ItemNameSelector { get; set; }

    public Func<object?, string>? ItemKeySelector { get; set; }

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

    public IList RawItems
    {
        get => _rawItems;
        set
        {
            if (_rawCollectionNotifier is not null)
                _rawCollectionNotifier.CollectionChanged -= OnRawCollectionChanged;

            _rawItems = value ?? new List<object>();

            _rawCollectionNotifier = _rawItems as INotifyCollectionChanged;
            if (_rawCollectionNotifier is not null)
                _rawCollectionNotifier.CollectionChanged += OnRawCollectionChanged;

            SetDiscovered(_rawItems);
        }
    }

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

    public void AddRuntime(object item, bool isSelected = false, bool canEdit = true, bool canRemove = true, Action<object>? onEdit = null, Action<object>? onRemove = null)
    {
        var key = GetItemKey(item);
        if (_discoveredItems.Any(d => string.Equals(GetItemKey(d), key, StringComparison.Ordinal)))
            return;

        var existing = _runtimeItems.FirstOrDefault(r => string.Equals(GetItemKey(r), key, StringComparison.Ordinal));
        if (existing is not null)
        {
            _runtimeItems.Remove(existing);
            _runtimeOptions.Remove(key);
            _selectedKeys.Remove(key);
        }

        _runtimeItems.Add(item);
        _runtimeOptions[key] = new RuntimeItemOptions(canEdit, canRemove, onEdit, onRemove);
        if (isSelected)
        {
            _selectedKeys.Add(key);
        }

        RebuildWrappedItems();
        SelectionChanged?.Invoke();
    }

    public void RemoveRuntime(object item)
    {
        var key = GetItemKey(item);
        var existing = _runtimeItems.FirstOrDefault(runtime => string.Equals(GetItemKey(runtime), key, StringComparison.Ordinal));
        if (existing is null)
        {
            return;
        }

        _runtimeItems.Remove(existing);
        _runtimeOptions.Remove(key);
        _selectedKeys.Remove(key);
        RebuildWrappedItems();
        SelectionChanged?.Invoke();
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
    private void AddRuntimeItem()
    {
        AddItemCommand?.Execute(null);
    }

    [RelayCommand]
    private void RemoveRuntimeItem(object? originalItem)
    {
        if (originalItem is null) return;

        var key = GetItemKey(originalItem);
        if (_runtimeOptions.TryGetValue(key, out var options))
        {
            options.RemoveRequested?.Invoke(originalItem);
        }

        RemoveRuntime(originalItem);
    }

    [RelayCommand]
    private void EditRuntimeItem(object? originalItem)
    {
        if (originalItem is null) return;

        var key = GetItemKey(originalItem);
        if (_runtimeOptions.TryGetValue(key, out var options) && options.CanEdit)
        {
            options.EditRequested?.Invoke(originalItem);
        }
    }

    public void ClearRuntime()
    {
        if (_runtimeItems.Count == 0)
            return;

        foreach (var item in _runtimeItems)
        {
            _selectedKeys.Remove(GetItemKey(item));
        }

        _runtimeItems.Clear();
        _runtimeOptions.Clear();
        RebuildWrappedItems();
        SelectionChanged?.Invoke();
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
                IsRuntime = false,
            });
        }

        foreach (var item in _runtimeItems)
        {
            var name = ItemNameSelector?.Invoke(item) ?? item?.ToString() ?? "";
            if (ShouldFilter(name, search))
                continue;

            var key = GetItemKey(item);
            var options = _runtimeOptions.GetValueOrDefault(key) ?? new RuntimeItemOptions(false, false);
            _allWrapped.Add(new WrappedListItem(item, name, false, name)
            {
                IsSelected = _selectedKeys.Contains(key),
                IsRuntime = true,
                CanEdit = options.CanEdit,
                CanRemove = options.CanRemove,
            });
        }

        OnPropertyChanged(nameof(FilteredItems));
    }

    private static bool ShouldFilter(string name, string search)
    {
        return !string.IsNullOrWhiteSpace(search) &&
               !name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void OnRawCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SetDiscovered(_rawItems);
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
            .Concat(_runtimeItems.Select(GetItemKey))
            .ToHashSet(StringComparer.Ordinal);

        _selectedKeys.RemoveWhere(key => !knownKeys.Contains(key));
    }

    private sealed record RuntimeItemOptions(bool CanEdit, bool CanRemove, Action<object>? EditRequested = null, Action<object>? RemoveRequested = null);
}
