using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class MessagesPanelViewModel : ObservableObject
{
    public MessagesPanelViewModel(IWorkspaceStore workspaceStore)
    {
        WorkspaceStore = workspaceStore;
        WorkspaceStore.StateChanged += (_, _) => SyncFromCurrentState();
        SyncFromCurrentState();
    }

    public IWorkspaceStore WorkspaceStore { get; }

    public IAsyncRelayCommand? BuildPlanCommand { get; set; }

    public IAsyncRelayCommand? ApplyCommand { get; set; }

    public ObservableCollection<ArchitectureWarning> ArchitectureWarnings { get; } = [];

    public ObservableCollection<GenerationWarning> GenerationWarnings { get; } = [];

    [ObservableProperty]
    private bool _canBuildPlan;

    [ObservableProperty]
    private bool _canApply;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private int _architectureWarningCount;

    [ObservableProperty]
    private int _generationWarningCount;

    public bool HasErrorText => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasArchitectureWarnings => ArchitectureWarningCount > 0;

    public bool HasGenerationWarnings => GenerationWarningCount > 0;

    private void SyncFromCurrentState()
    {
        var state = WorkspaceStore.State;
        StatusText = state.StatusText;
        CanBuildPlan = state.CanBuildPlan;
        CanApply = state.CanApplyPlan;
        ErrorText = state.PlanBuildErrorMessage;

        ArchitectureWarnings.Clear();
        foreach (var warning in state.Warnings)
        {
            ArchitectureWarnings.Add(warning);
        }

        GenerationWarnings.Clear();
        foreach (var warning in state.GenerationWarnings)
        {
            GenerationWarnings.Add(warning);
        }

        ArchitectureWarningCount = ArchitectureWarnings.Count;
        GenerationWarningCount = GenerationWarnings.Count;
        OnPropertyChanged(nameof(HasArchitectureWarnings));
        OnPropertyChanged(nameof(HasGenerationWarnings));
        OnPropertyChanged(nameof(HasErrorText));
    }
}
