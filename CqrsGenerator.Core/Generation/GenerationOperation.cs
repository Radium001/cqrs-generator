namespace CqrsGenerator.Core.Generation;

public enum GenerationOperationKind
{
    CreateFile,
    UpdateFile,
    DeleteFile,
    CreateDirectory,
}

public abstract record GenerationOperation(GenerationOperationKind Kind, string Path);

public sealed record CreateFileOperation(string Path, string Content)
    : GenerationOperation(GenerationOperationKind.CreateFile, Path);

public sealed record UpdateFileOperation(
    string Path,
    string Content,
    string? OriginalContent = null,
    Func<string, string>? Transform = null)
    : GenerationOperation(GenerationOperationKind.UpdateFile, Path);

public sealed record DeleteFileOperation(string Path, string? OriginalContent = null)
    : GenerationOperation(GenerationOperationKind.DeleteFile, Path);

public sealed record CreateDirectoryOperation(string Path)
    : GenerationOperation(GenerationOperationKind.CreateDirectory, Path);

public sealed record GenerationWarning(string Message);

public sealed record GenerationConflict(string Path, string Message);
