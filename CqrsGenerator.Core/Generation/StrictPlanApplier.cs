namespace CqrsGenerator.Core.Generation;

public sealed class StrictPlanApplier
{
    private readonly IPreparedApplyFileSystem _fileSystem;

    public StrictPlanApplier(IPreparedApplyFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new LocalPreparedApplyFileSystem();
    }

    public void Apply(PreparedApplyPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.HasConflicts)
        {
            var conflicts = string.Join(
                Environment.NewLine,
                package.Conflicts.Select(conflict => $" - {conflict.Path}: {conflict.Message}"));
            throw new InvalidOperationException($"Generation stopped because the prepared package has conflicts:{Environment.NewLine}{conflicts}");
        }

        ValidatePreconditions(package);

        var journal = new List<IJournalEntry>();
        try
        {
            foreach (var operation in package.Operations)
            {
                ApplyOperation(operation, journal);
            }
        }
        catch (Exception ex)
        {
            try
            {
                Rollback(journal);
            }
            catch (Exception rollbackEx)
            {
                throw new InvalidOperationException("Apply failed and rollback did not complete successfully.", new AggregateException(ex, rollbackEx));
            }

            throw new InvalidOperationException("Apply failed. All changes from the current transaction were rolled back.", ex);
        }
    }

    private void ValidatePreconditions(PreparedApplyPackage package)
    {
        var knownDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in package.Operations)
        {
            switch (operation)
            {
                case PreparedCreateDirectoryOperation createDirectory:
                    if (_fileSystem.FileExists(createDirectory.Path))
                    {
                        throw new InvalidOperationException($"Cannot create directory '{createDirectory.RelativePath}' because a file already exists at that path.");
                    }

                    knownDirectories.Add(createDirectory.Path);
                    break;

                case PreparedCreateFileOperation createFile:
                    if (_fileSystem.FileExists(createFile.Path))
                    {
                        throw new InvalidOperationException($"Cannot create file '{createFile.RelativePath}' because it already exists.");
                    }

                    EnsureParentDirectoryAvailable(createFile.Path, createFile.RelativePath, knownDirectories);
                    break;

                case PreparedUpdateFileOperation updateFile:
                    if (!_fileSystem.FileExists(updateFile.Path))
                    {
                        throw new InvalidOperationException($"Cannot update file '{updateFile.RelativePath}' because it no longer exists.");
                    }

                    EnsureParentDirectoryAvailable(updateFile.Path, updateFile.RelativePath, knownDirectories);
                    if (updateFile.OriginalContent is null)
                    {
                        throw new InvalidOperationException($"Cannot update file '{updateFile.RelativePath}' because the original content snapshot is missing.");
                    }

                    var currentContent = _fileSystem.ReadAllText(updateFile.Path);
                    if (!string.Equals(currentContent, updateFile.OriginalContent, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Cannot update file '{updateFile.RelativePath}' because it changed after the plan was built.");
                    }

                    break;

                case PreparedDeleteFileOperation deleteFile:
                    if (!_fileSystem.FileExists(deleteFile.Path))
                    {
                        throw new InvalidOperationException($"Cannot delete file '{deleteFile.RelativePath}' because it no longer exists.");
                    }

                    EnsureParentDirectoryAvailable(deleteFile.Path, deleteFile.RelativePath, knownDirectories);
                    if (deleteFile.OriginalContent is null)
                    {
                        throw new InvalidOperationException($"Cannot delete file '{deleteFile.RelativePath}' because the original content snapshot is missing.");
                    }

                    var currentDeleteContent = _fileSystem.ReadAllText(deleteFile.Path);
                    if (!string.Equals(currentDeleteContent, deleteFile.OriginalContent, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Cannot delete file '{deleteFile.RelativePath}' because it changed after the plan was built.");
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unsupported prepared operation: {operation.GetType().Name}");
            }
        }
    }

    private void ApplyOperation(PreparedApplyOperation operation, ICollection<IJournalEntry> journal)
    {
        switch (operation)
        {
            case PreparedCreateDirectoryOperation createDirectory:
                if (!_fileSystem.DirectoryExists(createDirectory.Path))
                {
                    _fileSystem.CreateDirectory(createDirectory.Path);
                    journal.Add(new CreatedDirectoryJournalEntry(createDirectory.Path));
                }

                break;

            case PreparedCreateFileOperation createFile:
                _fileSystem.WriteAllText(createFile.Path, createFile.Content);
                journal.Add(new CreatedFileJournalEntry(createFile.Path));
                break;

            case PreparedUpdateFileOperation updateFile:
                journal.Add(new UpdatedFileJournalEntry(updateFile.Path, updateFile.OriginalContent ?? string.Empty));
                _fileSystem.WriteAllText(updateFile.Path, updateFile.Content);
                break;

            case PreparedDeleteFileOperation deleteFile:
                journal.Add(new DeletedFileJournalEntry(deleteFile.Path, deleteFile.OriginalContent ?? string.Empty));
                _fileSystem.DeleteFile(deleteFile.Path);
                break;

            default:
                throw new InvalidOperationException($"Unsupported prepared operation: {operation.GetType().Name}");
        }
    }

    private void Rollback(IEnumerable<IJournalEntry> journal)
    {
        foreach (var entry in journal.Reverse())
        {
            entry.Rollback(_fileSystem);
        }
    }

    private void EnsureParentDirectoryAvailable(string path, string relativePath, ISet<string> knownDirectories)
    {
        var parentDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return;
        }

        if (_fileSystem.DirectoryExists(parentDirectory) || knownDirectories.Contains(parentDirectory))
        {
            return;
        }

        throw new InvalidOperationException($"Cannot write '{relativePath}' because its parent directory is not part of the prepared package.");
    }

    private interface IJournalEntry
    {
        void Rollback(IPreparedApplyFileSystem fileSystem);
    }

    private sealed record CreatedDirectoryJournalEntry(string Path) : IJournalEntry
    {
        public void Rollback(IPreparedApplyFileSystem fileSystem)
        {
            if (fileSystem.DirectoryExists(Path) && fileSystem.IsDirectoryEmpty(Path))
            {
                fileSystem.DeleteDirectory(Path);
            }
        }
    }

    private sealed record CreatedFileJournalEntry(string Path) : IJournalEntry
    {
        public void Rollback(IPreparedApplyFileSystem fileSystem)
        {
            if (fileSystem.FileExists(Path))
            {
                fileSystem.DeleteFile(Path);
            }
        }
    }

    private sealed record UpdatedFileJournalEntry(string Path, string OriginalContent) : IJournalEntry
    {
        public void Rollback(IPreparedApplyFileSystem fileSystem)
        {
            fileSystem.WriteAllText(Path, OriginalContent);
        }
    }

    private sealed record DeletedFileJournalEntry(string Path, string OriginalContent) : IJournalEntry
    {
        public void Rollback(IPreparedApplyFileSystem fileSystem)
        {
            fileSystem.WriteAllText(Path, OriginalContent);
        }
    }
}
