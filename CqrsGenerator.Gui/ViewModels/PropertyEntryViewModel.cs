using CommunityToolkit.Mvvm.ComponentModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class PropertyEntryViewModel : ObservableObject
{
    public PropertyEntryViewModel(string type, string name)
    {
        _type = type;
        _name = name;
    }

    [ObservableProperty]
    private string _type;

    [ObservableProperty]
    private string _name;

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Type) &&
        !string.IsNullOrWhiteSpace(Name);
}
