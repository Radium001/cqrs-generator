namespace CqrsGenerator.Gui.ViewModels;

public enum ChangedLineKind
{
    Added,
    Modified,
}

public sealed record ChangedLineInfo(int LineIndex, ChangedLineKind Kind);
