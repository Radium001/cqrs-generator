using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class PropertyEntryListEditorViewModel
{
    public PropertyEntryListEditorViewModel(
        ObservableCollection<PropertyEntryViewModel> entries,
        IRelayCommand addEntryCommand,
        IRelayCommand<PropertyEntryViewModel> removeEntryCommand,
        string title,
        string addButtonText,
        string typeWatermark,
        string nameWatermark)
    {
        Entries = entries;
        AddEntryCommand = addEntryCommand;
        RemoveEntryCommand = removeEntryCommand;
        Title = title;
        AddButtonText = addButtonText;
        TypeWatermark = typeWatermark;
        NameWatermark = nameWatermark;
    }

    public ObservableCollection<PropertyEntryViewModel> Entries { get; }

    public IRelayCommand AddEntryCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemoveEntryCommand { get; }

    public string Title { get; }

    public string AddButtonText { get; }

    public string TypeWatermark { get; }

    public string NameWatermark { get; }
}
