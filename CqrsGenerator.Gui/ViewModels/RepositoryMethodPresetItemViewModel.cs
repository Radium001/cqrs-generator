using CommunityToolkit.Mvvm.ComponentModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class RepositoryMethodPresetItemViewModel : ObservableObject
{
    public RepositoryMethodPresetItemViewModel(string key, string name, bool isSelected)
    {
        Key = key;
        Name = name;
        _isSelected = isSelected;
    }

    public string Key { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;
}
