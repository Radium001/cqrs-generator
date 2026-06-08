namespace CqrsGenerator.Core.Generation;

public sealed class GenerationPlan
{
    private readonly List<GenerationOperation> _operations = [];
    private readonly List<GenerationWarning> _warnings = [];
    private readonly List<GenerationConflict> _conflicts = [];

    public IReadOnlyList<GenerationOperation> Operations => _operations;

    public IReadOnlyList<GenerationWarning> Warnings => _warnings;

    public IReadOnlyList<GenerationConflict> Conflicts => _conflicts;

    public bool HasConflicts => _conflicts.Count > 0;

    public IReadOnlyList<PlannedFile> Files => _operations
        .OfType<CreateFileOperation>()
        .Select(operation => new PlannedFile(operation.Path, operation.Content))
        .ToArray();

    public void AddFile(string path, string content)
    {
        AddCreateFile(path, content);
    }

    public void AddCreateFile(string path, string content)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (TryHandleDuplicateFileOperation(fullPath, content, GenerationOperationKind.CreateFile))
        {
            return;
        }

        if (File.Exists(fullPath))
        {
            AddConflict(fullPath, "Файл уже существует.");
        }

        _operations.Add(new CreateFileOperation(fullPath, content));
    }

    public void AddUpdateFile(string path, string content)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (TryHandleDuplicateFileOperation(fullPath, content, GenerationOperationKind.UpdateFile))
        {
            return;
        }

        string? originalContent = null;
        if (!File.Exists(fullPath))
        {
            AddConflict(fullPath, "Файл для изменения не найден.");
        }
        else
        {
            originalContent = File.ReadAllText(fullPath);
        }

        _operations.Add(new UpdateFileOperation(fullPath, content, originalContent));
    }

    public void AddDirectory(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (_operations.OfType<CreateDirectoryOperation>().Any(operation => SamePath(operation.Path, fullPath)))
        {
            return;
        }

        _operations.Add(new CreateDirectoryOperation(fullPath));
    }

    public void AddDeleteFile(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (FindFileOperationIndex(fullPath) >= 0)
        {
            AddConflict(fullPath, "Файл уже имеет запланированную файловую операцию, удаление невозможно объединить автоматически.");
            return;
        }

        string? originalContent = null;
        if (!File.Exists(fullPath))
        {
            AddConflict(fullPath, "Файл для удаления не найден.");
        }
        else
        {
            originalContent = File.ReadAllText(fullPath);
        }

        _operations.Add(new DeleteFileOperation(fullPath, originalContent));
    }

    public void AddWarning(string message)
    {
        _warnings.Add(new GenerationWarning(message));
    }

    public void AddConflict(string path, string message)
    {
        _conflicts.Add(new GenerationConflict(System.IO.Path.GetFullPath(path), message));
    }

    public void Merge(GenerationPlan other)
    {
        foreach (var operation in other.Operations)
        {
            switch (operation)
            {
                case CreateFileOperation createFile:
                    if (!TryHandleDuplicateFileOperation(createFile.Path, createFile.Content, GenerationOperationKind.CreateFile))
                        _operations.Add(createFile);
                    break;
                case UpdateFileOperation updateFile:
                    if (updateFile.Transform is not null)
                    {
                        TransformFile(updateFile.Path, updateFile.Transform);
                    }
                    else if (!TryHandleDuplicateFileOperation(updateFile.Path, updateFile.Content, GenerationOperationKind.UpdateFile))
                    {
                        _operations.Add(updateFile);
                    }

                    break;
                case CreateDirectoryOperation createDirectory:
                    AddDirectory(createDirectory.Path);
                    break;
                case DeleteFileOperation deleteFile:
                    AddDeleteFile(deleteFile.Path);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported generation operation: {operation.GetType().Name}");
            }
        }

        _warnings.AddRange(other.Warnings);
        _conflicts.AddRange(other.Conflicts);
        DeduplicateConflicts();
    }

    public void TransformFile(string path, Func<string, string> transform)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        var existingIndex = FindFileOperationIndex(fullPath);
        if (existingIndex >= 0)
        {
            switch (_operations[existingIndex])
            {
                case CreateFileOperation createFile:
                    _operations[existingIndex] = createFile with { Content = transform(createFile.Content) };
                    return;
                case UpdateFileOperation updateFile:
                    var newContent = transform(updateFile.Content);
                    _operations[existingIndex] = updateFile with
                    {
                        Content = newContent,
                        Transform = ComposeTransform(updateFile.Transform, transform),
                    };
                    return;
            }
        }

        if (!File.Exists(fullPath))
        {
            AddConflict(fullPath, "Файл для изменения не найден.");
            return;
        }

        var originalContent = File.ReadAllText(fullPath);
        var transformedContent = transform(originalContent);
        if (string.Equals(originalContent, transformedContent, StringComparison.Ordinal))
        {
            return;
        }

        _operations.Add(new UpdateFileOperation(fullPath, transformedContent, originalContent, transform));
    }

    private static Func<string, string>? ComposeTransform(Func<string, string>? existing, Func<string, string> next)
    {
        if (existing is null)
            return next;

        return content => next(existing(content));
    }

    public bool TryGetPlannedFileContent(string path, out string content)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        var existingIndex = FindFileOperationIndex(fullPath);
        if (existingIndex >= 0)
        {
            content = _operations[existingIndex] switch
            {
                CreateFileOperation createFile => createFile.Content,
                UpdateFileOperation updateFile => updateFile.Content,
                DeleteFileOperation => "",
                _ => "",
            };
            return true;
        }

        content = "";
        return false;
    }

    private bool TryHandleDuplicateFileOperation(string fullPath, string content, GenerationOperationKind newKind)
    {
        var existingIndex = FindFileOperationIndex(fullPath);
        if (existingIndex < 0)
        {
            return false;
        }

        var existing = _operations[existingIndex];
        var existingContent = existing switch
        {
            CreateFileOperation createFile => createFile.Content,
            UpdateFileOperation updateFile => updateFile.Content,
            DeleteFileOperation => null,
            _ => null,
        };

        if (existingContent == content)
        {
            return true;
        }

        AddConflict(fullPath, $"Файл уже имеет запланированную операцию '{existing.Kind}', новая операция '{newKind}' содержит другое содержимое.");
        return true;
    }

    private int FindFileOperationIndex(string fullPath)
    {
        for (var i = 0; i < _operations.Count; i++)
        {
            if ((_operations[i].Kind is GenerationOperationKind.CreateFile
                or GenerationOperationKind.UpdateFile
                or GenerationOperationKind.DeleteFile)
                && SamePath(_operations[i].Path, fullPath))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            System.IO.Path.GetFullPath(left),
            System.IO.Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private void DeduplicateConflicts()
    {
        var seen = new HashSet<(string Path, string Message)>();
        for (var i = _conflicts.Count - 1; i >= 0; i--)
        {
            if (!seen.Add((_conflicts[i].Path, _conflicts[i].Message)))
                _conflicts.RemoveAt(i);
        }
    }
}
