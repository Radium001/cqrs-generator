using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public partial class WrappedListPickerViewModel : ObservableObject
{
    private IList _rawItems = new List<object>();
    private readonly ObservableCollection<WrappedListItem> _allWrapped = [];
    private INotifyCollectionChanged? _rawCollectionNotifier;

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

    public Func<object?, string>? ItemNameSelector { get; set; }
    public Func<object?, string?>? ItemPrimaryTextSelector { get; set; }
    public Func<object?, string?>? ItemSecondaryTextSelector { get; set; }
    public Func<object?, WrappedListAccentKind>? ItemAccentKindSelector { get; set; }
    public Func<object?, bool>? ItemSelectableSelector { get; set; }
    public Func<object?, string?>? ItemSelectionBlockedReasonSelector { get; set; }
    public Func<object?, string>? ItemSearchTextSelector { get; set; }

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

    // Kept for backward compat when consumer sets RawItems directly
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

            RebuildWrappedItems();
        }
    }

    public void SetDiscovered(IEnumerable<AvailableArtifactItem> items)
    {
        Items = items.ToList();
        if (ItemNameSelector is null)
            ItemNameSelector = item => item is AvailableArtifactItem a ? a.Name : item?.ToString() ?? string.Empty;
    }

    private void OnRawCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildWrappedItems();
    }

    public string SearchWatermark => SelectedItem?.IsCustom == true ? CustomEntryWatermark : "Search...";

    public IEnumerable FilteredItems
    {
        get
        {
            if (IsSelectionLocked && _lockedOriginalItem is not null)
            {
                return _allWrapped.Where(w => !w.IsCustom && ReferenceEquals(w.OriginalItem, _lockedOriginalItem));
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

        if (IsSelectionLocked && _lockedOriginalItem is not null && value?.OriginalItem != _lockedOriginalItem)
        {
            var match = _allWrapped.FirstOrDefault(w => !w.IsCustom && ReferenceEquals(w.OriginalItem, _lockedOriginalItem));
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
        if (IsSelectionLocked && _lockedOriginalItem is not null)
        {
            var match = _allWrapped.FirstOrDefault(w => !w.IsCustom && ReferenceEquals(w.OriginalItem, _lockedOriginalItem));
            if (match is not null)
            {
                SelectedItem = match;
                return;
            }
        }

        if (previousSelection is not null)
        {
            // Don't restore custom selection if regular items are now available
            if (!previousSelection.IsCustom || !_allWrapped.Any(w => !w.IsCustom))
            {
                var match = _allWrapped.FirstOrDefault(w =>
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

        var match = _allWrapped.FirstOrDefault(w => !w.IsCustom && ReferenceEquals(w.OriginalItem, rawItem));
        if (match is not null)
            SelectedItem = match;
    }

    public void LockSelection(object originalItem)
    {
        _lockedOriginalItem = originalItem;
        var name = ItemNameSelector?.Invoke(originalItem) ?? originalItem.ToString() ?? "";
        IsSelectionLocked = true;
        SearchText = name;
        SelectRawItem(originalItem);
    }

    public void UnlockSelection()
    {
        _lockedOriginalItem = null;
        IsSelectionLocked = false;
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
