using System.Collections.ObjectModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class ActionGroupViewModel
{
    public ActionGroupViewModel(string title, IEnumerable<ActionLauncherItemViewModel> actions)
    {
        Title = title;
        Actions = [.. actions];
    }

    public string Title { get; }

    public ObservableCollection<ActionLauncherItemViewModel> Actions { get; }
}
