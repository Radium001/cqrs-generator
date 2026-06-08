namespace CqrsGenerator.Core.Generation;

public abstract record PreparedApplyOperation(GenerationOperationKind Kind, string Path, string RelativePath);

public sealed record PreparedCreateDirectoryOperation(string Path, string RelativePath)
    : PreparedApplyOperation(GenerationOperationKind.CreateDirectory, Path, RelativePath);

public sealed record PreparedCreateFileOperation(string Path, string RelativePath, string Content)
    : PreparedApplyOperation(GenerationOperationKind.CreateFile, Path, RelativePath);

public sealed record PreparedUpdateFileOperation(
    string Path,
    string RelativePath,
    string Content,
    string? OriginalContent,
    string? OriginalContentHash)
    : PreparedApplyOperation(GenerationOperationKind.UpdateFile, Path, RelativePath);

public sealed record PreparedDeleteFileOperation(
    string Path,
    string RelativePath,
    string? OriginalContent,
    string? OriginalContentHash)
    : PreparedApplyOperation(GenerationOperationKind.DeleteFile, Path, RelativePath);
