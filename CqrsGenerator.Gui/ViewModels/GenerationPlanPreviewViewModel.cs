using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class GenerationPlanPreviewViewModel : ObservableObject
{
    public GenerationPlanPreviewViewModel(IWorkspaceStore workspaceStore)
    {
        WorkspaceStore = workspaceStore;
        WorkspaceStore.StateChanged += (_, state) => ApplySnapshot(state.CurrentPlanPreviewSnapshot);
        SummaryItems = [];
        RootNodes = new ObservableCollection<PlanTreeNodeViewModel>();
        SelectedPreview = new FilePreviewViewModel(FilePreviewKind.None, null, "Build plan to see affected files.");
        EmptyStateText = "Build plan to see affected files.";
        ApplySnapshot(WorkspaceStore.State.CurrentPlanPreviewSnapshot);
    }

    public IWorkspaceStore WorkspaceStore { get; }

    public ObservableCollection<PreviewSummaryItemViewModel> SummaryItems { get; }

    public ObservableCollection<PlanTreeNodeViewModel> RootNodes { get; }

    [ObservableProperty]
    private PlanTreeNodeViewModel? _selectedPlanNode;

    [ObservableProperty]
    private FilePreviewViewModel _selectedPreview;

    [ObservableProperty]
    private string _emptyStateText = string.Empty;

    [ObservableProperty]
    private bool _isErrorState;

    public string DetailsTitle => "Code Preview";

    public bool HasPlan => RootNodes.Count > 0;

    public bool ShowEmptyState => !HasPlan;

    partial void OnSelectedPlanNodeChanged(PlanTreeNodeViewModel? value)
    {
        SelectedPreview = value?.Preview ?? new FilePreviewViewModel(FilePreviewKind.None, null, "Select a file to preview.");
    }

    private void ApplySnapshot(PlanPreviewSnapshot snapshot)
    {
        SummaryItems.Clear();
        RootNodes.Clear();

        foreach (var item in snapshot.SummaryItems)
        {
            SummaryItems.Add(item);
        }

        foreach (var node in snapshot.RootNodes)
        {
            RootNodes.Add(node);
        }

        EmptyStateText = snapshot.EmptyStateText;
        IsErrorState = snapshot.IsError;
        SelectedPlanNode = RootNodes.FirstOrDefault();
        SelectedPreview = SelectedPlanNode?.Preview ?? snapshot.DefaultPreview;
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
