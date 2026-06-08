using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CqrsGenerator.Gui.ViewModels;

public partial class CyclicInputViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<string>? _prefixes;

    [ObservableProperty]
    private IReadOnlyList<string>? _suffixes;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _text = "";

    public string CurrentPrefix
    {
        get
        {
            if (Prefixes is not { Count: > 0 }) return "";
            if (SelectedIndex < 0 || SelectedIndex >= Prefixes.Count) return "";
            return Prefixes[SelectedIndex];
        }
    }

    public string CurrentSuffix
    {
        get
        {
            if (Suffixes is not { Count: > 0 }) return "";
            if (SelectedIndex < 0 || SelectedIndex >= Suffixes.Count) return "";
            return Suffixes[SelectedIndex];
        }
    }

    public bool HasPrefix => !string.IsNullOrEmpty(CurrentPrefix);

    public bool HasSuffix => !string.IsNullOrEmpty(CurrentSuffix);

    public string FullText => CurrentPrefix + Text + CurrentSuffix;

    partial void OnSelectedIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentPrefix));
        OnPropertyChanged(nameof(CurrentSuffix));
        OnPropertyChanged(nameof(HasPrefix));
        OnPropertyChanged(nameof(HasSuffix));
        OnPropertyChanged(nameof(FullText));
    }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(FullText));
    }

    partial void OnPrefixesChanged(IReadOnlyList<string>? value)
    {
        OnPropertyChanged(nameof(CurrentPrefix));
        OnPropertyChanged(nameof(HasPrefix));
    }

    partial void OnSuffixesChanged(IReadOnlyList<string>? value)
    {
        OnPropertyChanged(nameof(CurrentSuffix));
        OnPropertyChanged(nameof(HasSuffix));
    }

    [RelayCommand]
    private void Advance() => CycleForward();

    public void CycleForward()
    {
        var max = MaxIndex();
        if (max <= 0) return;
        SelectedIndex = (SelectedIndex + 1) % max;
    }

    public void CycleBackward()
    {
        var max = MaxIndex();
        if (max <= 0) return;
        SelectedIndex = (SelectedIndex - 1 + max) % max;
    }

    private int MaxIndex()
    {
        var prefixCount = Prefixes?.Count ?? 0;
        var suffixCount = Suffixes?.Count ?? 0;
        return Math.Max(prefixCount, suffixCount);
    }
}
