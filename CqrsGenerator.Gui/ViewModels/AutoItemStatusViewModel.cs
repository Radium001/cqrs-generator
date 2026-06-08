using CommunityToolkit.Mvvm.ComponentModel;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class AutoItemStatusViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayText = "";

    [ObservableProperty]
    private AutoItemStatus _status;

    public bool IsCreated => Status == AutoItemStatus.Created;

    public bool IsModified => Status == AutoItemStatus.Modified;

    public string DescriptionText => Status switch
    {
        AutoItemStatus.Created => "will be created",
        AutoItemStatus.Modified => "will be extended",
        _ => "",
    };

    partial void OnStatusChanged(AutoItemStatus value)
    {
        OnPropertyChanged(nameof(IsCreated));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(DescriptionText));
    }
}
