using CommunityToolkit.Mvvm.ComponentModel;

namespace CqrsGenerator.Gui.Services;

public sealed partial class WorkspaceStore : ObservableObject, IWorkspaceStore
{
    [ObservableProperty]
    private WorkspaceState _state = WorkspaceState.Empty;

    public event EventHandler<WorkspaceState>? StateChanged;

    partial void OnStateChanged(WorkspaceState value)
    {
        StateChanged?.Invoke(this, value);
    }

    public void SetState(WorkspaceState state)
    {
        State = state;
    }
}
