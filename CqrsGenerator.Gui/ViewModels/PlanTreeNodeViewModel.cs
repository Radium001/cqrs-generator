using System.Collections.ObjectModel;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class PlanTreeNodeViewModel
{
    public PlanTreeNodeViewModel(
        string name,
        string? relativePath,
        PlanTreeNodeKind nodeKind,
        PlanPreviewOperationKind operationKind,
        IReadOnlyList<PlanTreeNodeViewModel> children,
        FilePreviewViewModel preview)
    {
        Name = name;
        RelativePath = relativePath;
        NodeKind = nodeKind;
        OperationKind = operationKind;
        Children = new ObservableCollection<PlanTreeNodeViewModel>(children);
        Preview = preview;
    }

    public string Name { get; }

    public string? RelativePath { get; }

    public PlanTreeNodeKind NodeKind { get; }

    public PlanPreviewOperationKind OperationKind { get; }

    public ObservableCollection<PlanTreeNodeViewModel> Children { get; }

    public FilePreviewViewModel Preview { get; }

    public bool IsFolder => NodeKind == PlanTreeNodeKind.Folder;

    public bool IsFile => NodeKind == PlanTreeNodeKind.File;

    public string Prefix => OperationKind switch
    {
        PlanPreviewOperationKind.CreateFile => "+",
        PlanPreviewOperationKind.UpdateFile => "~",
        PlanPreviewOperationKind.DeleteFile => "-",
        PlanPreviewOperationKind.CreateDirectory => "+",
        PlanPreviewOperationKind.Conflict => "x",
        _ => IsFolder ? "" : "•",
    };

    public string DisplayName => string.IsNullOrWhiteSpace(Prefix)
        ? Name
        : $"{Prefix} {Name}";

    public bool IsCreateOperation => OperationKind is PlanPreviewOperationKind.CreateFile or PlanPreviewOperationKind.CreateDirectory;

    public bool IsUpdateOperation => OperationKind == PlanPreviewOperationKind.UpdateFile;

    public bool IsDeleteOperation => OperationKind == PlanPreviewOperationKind.DeleteFile;

    public bool IsConflictOperation => OperationKind == PlanPreviewOperationKind.Conflict;
}
