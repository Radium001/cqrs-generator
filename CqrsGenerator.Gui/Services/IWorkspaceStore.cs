using System.ComponentModel;

namespace CqrsGenerator.Gui.Services;

public interface IWorkspaceStore : INotifyPropertyChanged
{
    WorkspaceState State { get; }

    event EventHandler<WorkspaceState>? StateChanged;

    void SetState(WorkspaceState state);
}
