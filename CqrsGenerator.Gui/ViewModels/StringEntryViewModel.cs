using CommunityToolkit.Mvvm.ComponentModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class StringEntryViewModel : ObservableObject
{
    public StringEntryViewModel(string value)
    {
        _value = value;
    }

    [ObservableProperty]
    private string _value;

    public bool IsComplete => !string.IsNullOrWhiteSpace(Value);
}
