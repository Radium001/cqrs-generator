using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class WrappedListItem : INotifyPropertyChanged
{
    private string _displayText;
    private string _baseName;
    private string? _iconGeometry;
    private bool _isSelected;
    private bool _isRuntime;
    private bool _canEdit;
    private bool _canRemove;
    private string? _primaryText;
    private string? _secondaryText;
    private WrappedListAccentKind _accentKind;
    private bool _isSelectable = true;
    private string? _selectionBlockedReason;
    private string? _searchText;

    public WrappedListItem(object? originalItem, string baseName, bool isCustom, string displayText, string? iconGeometry = null)
    {
        OriginalItem = originalItem;
        _baseName = baseName;
        IsCustom = isCustom;
        _displayText = displayText;
        _iconGeometry = iconGeometry;
        _primaryText = displayText;
        _searchText = displayText;
    }

    public object? OriginalItem { get; }

    public string BaseName
    {
        get => _baseName;
        set
        {
            if (_baseName != value)
            {
                _baseName = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCustom { get; }

    public string DisplayText
    {
        get => _displayText;
        set
        {
            if (_displayText != value)
            {
                _displayText = value;
                OnPropertyChanged();
            }
        }
    }

    public string? IconGeometry
    {
        get => _iconGeometry;
        set
        {
            if (_iconGeometry != value)
            {
                _iconGeometry = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRuntime
    {
        get => _isRuntime;
        set
        {
            if (_isRuntime != value)
            {
                _isRuntime = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CanEdit
    {
        get => _canEdit;
        set
        {
            if (_canEdit != value)
            {
                _canEdit = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CanRemove
    {
        get => _canRemove;
        set
        {
            if (_canRemove != value)
            {
                _canRemove = value;
                OnPropertyChanged();
            }
        }
    }

    public string? PrimaryText
    {
        get => _primaryText;
        set
        {
            if (_primaryText != value)
            {
                _primaryText = value;
                OnPropertyChanged();
            }
        }
    }

    public string? SecondaryText
    {
        get => _secondaryText;
        set
        {
            if (_secondaryText != value)
            {
                _secondaryText = value;
                OnPropertyChanged();
            }
        }
    }

    public WrappedListAccentKind AccentKind
    {
        get => _accentKind;
        set
        {
            if (_accentKind != value)
            {
                _accentKind = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWarningAccent));
            }
        }
    }

    public bool HasWarningAccent => AccentKind == WrappedListAccentKind.Warning;

    public bool HasWarningText => AccentKind == WrappedListAccentKind.Warning;

    public bool IsSelectable
    {
        get => _isSelectable;
        set
        {
            if (_isSelectable != value)
            {
                _isSelectable = value;
                OnPropertyChanged();
            }
        }
    }

    public string? SelectionBlockedReason
    {
        get => _selectionBlockedReason;
        set
        {
            if (_selectionBlockedReason != value)
            {
                _selectionBlockedReason = value;
                OnPropertyChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText ?? _displayText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }
    }

    public override string ToString() => DisplayText;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
