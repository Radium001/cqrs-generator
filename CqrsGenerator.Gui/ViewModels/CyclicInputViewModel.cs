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

    public void SetFullText(string value)
    {
        value ??= "";

        var selectedIndex = FindDecorationIndex(value);
        SelectedIndex = selectedIndex;

        var prefixLength = CurrentPrefix.Length;
        var suffixLength = CurrentSuffix.Length;
        Text = value.Substring(prefixLength, value.Length - prefixLength - suffixLength);
    }

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

    private int FindDecorationIndex(string value)
    {
        var fallbackIndex = 0;
        var bestIndex = -1;
        var bestDecorationLength = -1;

        for (var index = 0; index < MaxIndex(); index++)
        {
            var prefix = GetValue(Prefixes, index);
            var suffix = GetValue(Suffixes, index);
            if (prefix.Length == 0 && suffix.Length == 0)
            {
                fallbackIndex = index;
                continue;
            }

            if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
                !value.EndsWith(suffix, StringComparison.Ordinal) ||
                value.Length < prefix.Length + suffix.Length ||
                !HasNameBoundaryAfterPrefix(value, prefix))
            {
                continue;
            }

            var decorationLength = prefix.Length + suffix.Length;
            if (decorationLength > bestDecorationLength)
            {
                bestIndex = index;
                bestDecorationLength = decorationLength;
            }
        }

        return bestIndex >= 0 ? bestIndex : fallbackIndex;
    }

    private static string GetValue(IReadOnlyList<string>? values, int index)
        => values is not null && index >= 0 && index < values.Count ? values[index] : "";

    private static bool HasNameBoundaryAfterPrefix(string value, string prefix)
    {
        if (prefix.Length == 0 || value.Length == prefix.Length)
        {
            return true;
        }

        var lastPrefixCharacter = prefix[^1];
        if (!char.IsLetterOrDigit(lastPrefixCharacter))
        {
            return true;
        }

        var nextCharacter = value[prefix.Length];
        return char.IsUpper(nextCharacter) || char.IsDigit(nextCharacter) || nextCharacter == '_';
    }
}
