namespace CqrsGenerator.Gui.ViewModels;

public sealed class FilePreviewViewModel
{
    public FilePreviewViewModel(
        FilePreviewKind kind,
        string? relativePath,
        string? content,
        IReadOnlyList<ChangedLineInfo>? changedLines = null,
        IReadOnlyList<string>? messages = null)
    {
        Kind = kind;
        RelativePath = relativePath;
        Content = content;
        ChangedLines = changedLines ?? [];
        Messages = messages ?? [];
    }

    public FilePreviewKind Kind { get; }

    public string? RelativePath { get; }

    public string? Content { get; }

    public IReadOnlyList<ChangedLineInfo> ChangedLines { get; }

    public IReadOnlyList<string> Messages { get; }

    public bool IsEmpty => Kind == FilePreviewKind.None;

    public bool IsCreatedFile => Kind == FilePreviewKind.CreatedFile;

    public bool IsUpdatedFile => Kind == FilePreviewKind.UpdatedFile;

    public bool IsDeletedFile => Kind == FilePreviewKind.DeletedFile;

    public bool IsCreatedFileOrUpdatedFile => Kind is FilePreviewKind.CreatedFile or FilePreviewKind.UpdatedFile or FilePreviewKind.DeletedFile;

    public bool IsDirectory => Kind == FilePreviewKind.Directory;

    public bool IsConflict => Kind == FilePreviewKind.Conflict;
}
