using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public partial class WrappedListPickerViewModel : ObservableObject
{
    private IList _rawItems = new List<object>();
    private readonly ObservableCollection<WrappedListItem> _allWrapped = [];

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private WrappedListItem? _selectedItem;

    [ObservableProperty]
    private int _visibleItemCount = 5;

    [ObservableProperty]
    private IReadOnlyList<string>? _prefixes;

    [ObservableProperty]
    private int _selectedPrefixIndex;

    [ObservableProperty]
    private IReadOnlyList<string>? _suffixes;

    [ObservableProperty]
    private int _selectedSuffixIndex;

    [ObservableProperty]
    private bool _allowCustom;

    [ObservableProperty]
    private bool _isSelectionLocked;

    [ObservableProperty]
    private string _customEntryPrefix = "";

    [ObservableProperty]
    private string _customEntryWatermark = "Set type name";

    [ObservableProperty]
    private string? _customEntryIcon;

    [ObservableProperty]
    private string _customEntryLabel = "Custom...";

    private object? _lockedOriginalItem;
    private string? _lockedKey;
    private string? _selectedKey;

    public Func<object?, string>? ItemNameSelector { get; set; }
    public Func<object?, string?>? ItemPrimaryTextSelector { get; set; }
    public Func<object?, string?>? ItemSecondaryTextSelector { get; set; }
    public Func<object?, WrappedListAccentKind>? ItemAccentKindSelector { get; set; }
    public Func<object?, bool>? ItemSelectableSelector { get; set; }
    public Func<object?, string?>? ItemSelectionBlockedReasonSelector { get; set; }
    public Func<object?, string>? ItemSearchTextSelector { get; set; }
    public Func<object?, string>? ItemKeySelector { get; set; }

    public bool IsSearchReadOnly => IsSelectionLocked;

    public object? SelectedRawItem => SelectedItem?.OriginalItem;

    public string? SelectedBaseName
    {
        get
        {
            var baseName = SelectedItem?.BaseName;
            return string.IsNullOrWhiteSpace(baseName) ? null : baseName.Trim();
        }
    }

    public IList Items
    {
        get => _rawItems;
        set
        {
            _rawItems = value ?? new List<object>();
            RebuildWrappedItems();
        }
    }

    public void SetDiscovered(IEnumerable<AvailableArtifactItem> items)
    {
        Items = items.ToList();
        if (ItemNameSelector is null)
            ItemNameSelector = item => item is AvailableArtifactItem a ? a.Name : item?.ToString() ?? string.Empty;
    }

    public string SearchWatermark => SelectedItem?.IsCustom == true ? CustomEntryWatermark : "Search...";

    public IEnumerable FilteredItems
    {
        get
        {
            if (IsSelectionLocked && !string.IsNullOrWhiteSpace(_lockedKey))
            {
                return _allWrapped.Where(w => !w.IsCustom && string.Equals(GetItemKey(w.OriginalItem), _lockedKey, StringComparison.Ordinal));
            }

            var search = (SearchText ?? "").Trim();
            return _allWrapped.Where(w =>
                string.IsNullOrWhiteSpace(search) ||
                w.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (AllowCustom && _allWrapped.Count > 0 && _allWrapped[0].IsCustom)
        {
            var customItem = _allWrapped[0];
            var displayText = BuildCustomDisplayText(value);

            customItem.DisplayText = displayText;
            customItem.PrimaryText = displayText;
            customItem.SearchText = value ?? string.Empty;
            customItem.BaseName = value ?? "";
        }

        OnPropertyChanged(nameof(SelectedBaseName));
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(SearchWatermark));
        AutoSelectFirst();
    }

    partial void OnSelectedPrefixIndexChanged(int value)
    {
        RebuildWrappedItems();
    }

    partial void OnSelectedSuffixIndexChanged(int value)
    {
        RebuildWrappedItems();
    }

    partial void OnPrefixesChanged(IReadOnlyList<string>? value)
    {
        if (SelectedPrefixIndex >= (value?.Count ?? 0))
            SelectedPrefixIndex = 0;
        RebuildWrappedItems();
    }

    partial void OnSuffixesChanged(IReadOnlyList<string>? value)
    {
        if (SelectedSuffixIndex >= (value?.Count ?? 0))
            SelectedSuffixIndex = 0;
        RebuildWrappedItems();
    }

    partial void OnAllowCustomChanged(bool value)
    {
        RebuildWrappedItems();
    }

    partial void OnCustomEntryPrefixChanged(string value)
    {
        RebuildWrappedItems();
    }

    partial void OnCustomEntryIconChanged(string? value)
    {
        RebuildWrappedItems();
    }

    partial void OnIsSelectionLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(IsSearchReadOnly));
    }

    partial void OnSelectedItemChanged(WrappedListItem? value)
    {
        OnPropertyChanged(nameof(SearchWatermark));
        OnPropertyChanged(nameof(SelectedBaseName));

        if (value?.OriginalItem is not null)
        {
            _selectedKey = GetItemKey(value.OriginalItem);
        }
        else if (value?.IsCustom == true)
        {
            _selectedKey = null;
        }

        if (IsSelectionLocked && !string.IsNullOrWhiteSpace(_lockedKey) &&
            !string.Equals(GetItemKey(value?.OriginalItem), _lockedKey, StringComparison.Ordinal))
        {
            var match = _allWrapped.FirstOrDefault(w => !w.IsCustom && string.Equals(GetItemKey(w.OriginalItem), _lockedKey, StringComparison.Ordinal));
            if (match is not null)
            {
                _selectedItem = match;
                OnPropertyChanged(nameof(SelectedItem));
                OnPropertyChanged(nameof(SelectedRawItem));
            }
        }
    }

    private string GetCurrentPrefix()
    {
        if (Prefixes is not { Count: > 0 }) return "";
        if (SelectedPrefixIndex < 0 || SelectedPrefixIndex >= Prefixes.Count) return "";
        return Prefixes[SelectedPrefixIndex];
    }

    private string GetCurrentSuffix()
    {
        if (Suffixes is not { Count: > 0 }) return "";

        var prefix = GetCurrentPrefix();
        if (string.IsNullOrEmpty(prefix))
            return "";

        for (var i = 0; i < Suffixes.Count; i++)
            if (!string.IsNullOrEmpty(Suffixes[i]))
                return Suffixes[i];

        return "";
    }

    private string BuildCustomDisplayText(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return CustomEntryLabel;
        }

        return GetCurrentPrefix() + search + GetCurrentSuffix();
    }

    private void RebuildWrappedItems()
    {
        var previousSelection = SelectedItem;
        var previousKey = _selectedKey ?? (previousSelection?.OriginalItem is not null ? GetItemKey(previousSelection.OriginalItem) : null);

        _allWrapped.Clear();

        if (AllowCustom)
        {
            var search = SearchText ?? "";
            _allWrapped.Add(new WrappedListItem(
                null,
                search,
                true,
                BuildCustomDisplayText(search),
                CustomEntryIcon));
        }

        var prefix = GetCurrentPrefix();
        var suffix = GetCurrentSuffix();

        foreach (var item in _rawItems)
        {
            var name = ItemNameSelector?.Invoke(item) ?? item?.ToString() ?? "";
            var wrapped = new WrappedListItem(item, name, false, prefix + name + suffix)
            {
                PrimaryText = ItemPrimaryTextSelector?.Invoke(item) ?? (prefix + name + suffix),
                SecondaryText = ItemSecondaryTextSelector?.Invoke(item),
                AccentKind = ItemAccentKindSelector?.Invoke(item) ?? WrappedListAccentKind.None,
                IsSelectable = ItemSelectableSelector?.Invoke(item) ?? true,
                SelectionBlockedReason = ItemSelectionBlockedReasonSelector?.Invoke(item),
                SearchText = ItemSearchTextSelector?.Invoke(item) ?? (prefix + name + suffix),
            };
            _allWrapped.Add(wrapped);
        }

        OnPropertyChanged(nameof(FilteredItems));

        // If locked, force selection back to locked item
        if (IsSelectionLocked && !string.IsNullOrWhiteSpace(_lockedKey))
        {
            var match = _allWrapped.FirstOrDefault(w => !w.IsCustom && string.Equals(GetItemKey(w.OriginalItem), _lockedKey, StringComparison.Ordinal));
            if (match is not null)
            {
                SelectedItem = match;
                return;
            }
        }

        if (previousSelection is not null)
        {
            // Don't restore custom selection if regular items are now available.
            if (!previousSelection.IsCustom || !_allWrapped.Any(w => !w.IsCustom))
            {
                var match = !string.IsNullOrWhiteSpace(previousKey)
                    ? _allWrapped.FirstOrDefault(w => !w.IsCustom && string.Equals(GetItemKey(w.OriginalItem), previousKey, StringComparison.Ordinal))
                    : _allWrapped.FirstOrDefault(w =>
                        ReferenceEquals(w.OriginalItem, previousSelection.OriginalItem) &&
                        w.IsCustom == previousSelection.IsCustom);
                if (match is not null)
                {
                    SelectedItem = match;
                    return;
                }
            }
        }

        AutoSelectFirst();
    }

    private void AutoSelectFirst()
    {
        var filtered = FilteredItems.Cast<WrappedListItem>().ToList();
        if (filtered.Count == 0)
            return;

        // Prefer first regular (non-custom) item over the custom entry
        var firstRegular = filtered.FirstOrDefault(w => !w.IsCustom && w.IsSelectable);
        SelectedItem = firstRegular ?? filtered[0];
    }

    public void SelectRawItem(object? rawItem)
    {
        if (rawItem is null)
        {
            SelectedItem = null;
            return;
        }

        var key = GetItemKey(rawItem);
        SelectByKey(key);
    }

    public void SelectByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            SelectedItem = null;
            _selectedKey = null;
            return;
        }

        _selectedKey = key;
        var match = _allWrapped.FirstOrDefault(w => !w.IsCustom && string.Equals(GetItemKey(w.OriginalItem), key, StringComparison.Ordinal));
        if (match is not null)
            SelectedItem = match;
    }

    public void LockSelection(object originalItem)
    {
        _lockedOriginalItem = originalItem;
        _lockedKey = GetItemKey(originalItem);
        var name = ItemNameSelector?.Invoke(originalItem) ?? originalItem.ToString() ?? "";
        IsSelectionLocked = true;
        SearchText = name;
        SelectByKey(_lockedKey);
    }

    public void LockSelectionByKey(string key, string displayText)
    {
        _lockedOriginalItem = null;
        _lockedKey = key;
        IsSelectionLocked = true;
        SearchText = displayText;
        SelectByKey(key);
    }

    public void UnlockSelection(bool clearSearchText = false)
    {
        _lockedOriginalItem = null;
        _lockedKey = null;
        IsSelectionLocked = false;
        if (clearSearchText)
        {
            SearchText = string.Empty;
            SelectedItem = null;
            _selectedKey = null;
        }
    }

    private string GetItemKey(object? item)
    {
        return ItemKeySelector?.Invoke(item)
            ?? ItemNameSelector?.Invoke(item)
            ?? item?.ToString()
            ?? string.Empty;
    }

    public void RefreshFilteredItems()
    {
        OnPropertyChanged(nameof(FilteredItems));
    }

    public void SelectNext()
    {
        var filtered = FilteredItems.Cast<WrappedListItem>().ToList();
        if (filtered.Count == 0)
            return;

        if (SelectedItem is null)
        {
            SelectedItem = filtered[0];
            return;
        }

        var idx = filtered.IndexOf(SelectedItem);
        if (idx >= 0 && idx < filtered.Count - 1)
            SelectedItem = filtered[idx + 1];
    }

    public void SelectPrevious()
    {
        var filtered = FilteredItems.Cast<WrappedListItem>().ToList();
        if (filtered.Count == 0)
            return;

        if (SelectedItem is null)
        {
            SelectedItem = filtered[0];
            return;
        }

        var idx = filtered.IndexOf(SelectedItem);
        if (idx > 0)
            SelectedItem = filtered[idx - 1];
    }

    [RelayCommand]
    private void CyclePrefixForward()
    {
        if (Prefixes is { Count: > 0 })
            SelectedPrefixIndex = (SelectedPrefixIndex + 1) % Prefixes.Count;
    }

    [RelayCommand]
    private void CyclePrefixBackward()
    {
        if (Prefixes is { Count: > 0 })
            SelectedPrefixIndex = (SelectedPrefixIndex - 1 + Prefixes.Count) % Prefixes.Count;
    }

    public void CycleSuffixForward()
    {
        if (Suffixes is { Count: > 0 })
            SelectedSuffixIndex = (SelectedSuffixIndex + 1) % Suffixes.Count;
    }

    public void CycleSuffixBackward()
    {
        if (Suffixes is { Count: > 0 })
            SelectedSuffixIndex = (SelectedSuffixIndex - 1 + Suffixes.Count) % Suffixes.Count;
    }
}
