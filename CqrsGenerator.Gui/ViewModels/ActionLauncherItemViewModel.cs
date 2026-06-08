using CommunityToolkit.Mvvm.Input;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class ActionLauncherItemViewModel
{
    public ActionLauncherItemViewModel(GenerationActionDescriptor action, IRelayCommand openCommand)
    {
        Action = action;
        OpenCommand = openCommand;
    }

    public GenerationActionDescriptor Action { get; }

    public IRelayCommand OpenCommand { get; }

    public string DisplayName => Action.DisplayName;

    public string StatusText => Action.StatusText;

    public bool IsImplemented => Action.IsImplemented;
}
