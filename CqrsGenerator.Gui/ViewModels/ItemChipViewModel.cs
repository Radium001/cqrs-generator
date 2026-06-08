using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class ItemChipViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayText = "";

    public IRelayCommand? EditCommand { get; }

    public IRelayCommand? DeleteCommand { get; }

    public bool HasEdit => EditCommand is not null;

    public bool HasDelete => DeleteCommand is not null;

    public ItemChipViewModel(string displayText, Action? editAction = null, Action? deleteAction = null)
    {
        _displayText = displayText;
        if (editAction is not null)
            EditCommand = new RelayCommand(editAction);
        if (deleteAction is not null)
            DeleteCommand = new RelayCommand(deleteAction);
    }

    partial void OnDisplayTextChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }
}
