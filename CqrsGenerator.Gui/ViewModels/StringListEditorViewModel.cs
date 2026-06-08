using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class StringListEditorViewModel
{
    public StringListEditorViewModel(
        ObservableCollection<StringEntryViewModel> entries,
        IRelayCommand addEntryCommand,
        IRelayCommand<StringEntryViewModel> removeEntryCommand,
        string title,
        string addButtonText,
        string valueWatermark)
    {
        Entries = entries;
        AddEntryCommand = addEntryCommand;
        RemoveEntryCommand = removeEntryCommand;
        Title = title;
        AddButtonText = addButtonText;
        ValueWatermark = valueWatermark;
    }

    public ObservableCollection<StringEntryViewModel> Entries { get; }

    public IRelayCommand AddEntryCommand { get; }

    public IRelayCommand<StringEntryViewModel> RemoveEntryCommand { get; }

    public string Title { get; }

    public string AddButtonText { get; }

    public string ValueWatermark { get; }
}
