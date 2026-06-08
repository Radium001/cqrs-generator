using System.Security.Cryptography;
using System.Text;

namespace CqrsGenerator.Core.Generation;

public sealed class PlanPreparationService
{
    public PreparedApplyPackage Prepare(GenerationPlan plan, string targetRootPath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRootPath);

        var root = Path.GetFullPath(targetRootPath);
        var operations = new List<PreparedApplyOperation>();
        var conflicts = new List<GenerationConflict>(plan.Conflicts);
        var emittedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in plan.Operations)
        {
            switch (operation)
            {
                case CreateDirectoryOperation createDirectory:
                    if (!TryValidatePath(createDirectory.Path, root, conflicts))
                    {
                        continue;
                    }

                    EnsureImplicitParentDirectories(createDirectory.Path, root, operations, emittedDirectories);
                    AddExplicitDirectory(createDirectory.Path, root, operations, emittedDirectories);
                    break;

                case CreateFileOperation createFile:
                    if (!TryValidatePath(createFile.Path, root, conflicts))
                    {
                        continue;
                    }

                    EnsureImplicitParentDirectories(createFile.Path, root, operations, emittedDirectories);
                    operations.Add(new PreparedCreateFileOperation(
                        Path.GetFullPath(createFile.Path),
                        ToRelativePath(createFile.Path, root),
                        createFile.Content));
                    break;

                case UpdateFileOperation updateFile:
                    if (!TryValidatePath(updateFile.Path, root, conflicts))
                    {
                        continue;
                    }

                    operations.Add(new PreparedUpdateFileOperation(
                        Path.GetFullPath(updateFile.Path),
                        ToRelativePath(updateFile.Path, root),
                        updateFile.Content,
                        updateFile.OriginalContent,
                        updateFile.OriginalContent is null ? null : ComputeContentHash(updateFile.OriginalContent)));
                    break;

                case DeleteFileOperation deleteFile:
                    if (!TryValidatePath(deleteFile.Path, root, conflicts))
                    {
                        continue;
                    }

                    if (deleteFile.OriginalContent is null)
                    {
                        conflicts.Add(new GenerationConflict(Path.GetFullPath(deleteFile.Path), "Файл для удаления не найден."));
                    }

                    operations.Add(new PreparedDeleteFileOperation(
                        Path.GetFullPath(deleteFile.Path),
                        ToRelativePath(deleteFile.Path, root),
                        deleteFile.OriginalContent,
                        deleteFile.OriginalContent is null ? null : ComputeContentHash(deleteFile.OriginalContent)));
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported generation operation: {operation.GetType().Name}");
            }
        }

        var orderedOperations = operations.ToArray();
        return new PreparedApplyPackage
        {
            TargetRootPath = root,
            Fingerprint = ComputeFingerprint(root, orderedOperations, plan.Warnings, conflicts),
            Operations = orderedOperations,
            Warnings = plan.Warnings.ToArray(),
            Conflicts = conflicts.ToArray(),
        };
    }

    private static bool TryValidatePath(string path, string root, ICollection<GenerationConflict> conflicts)
    {
        var fullPath = Path.GetFullPath(path);
        if (IsUnderRoot(fullPath, root))
        {
            return true;
        }

        conflicts.Add(new GenerationConflict(fullPath, "Операция выходит за пределы выбранного корня проекта."));
        return false;
    }

    private static void EnsureImplicitParentDirectories(
        string path,
        string root,
        ICollection<PreparedApplyOperation> operations,
        ISet<string> emittedDirectories)
    {
        var parentDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return;
        }

        var stack = new Stack<string>();
        var current = Path.GetFullPath(parentDirectory);
        while (!SamePath(current, root)
               && !Directory.Exists(current)
               && emittedDirectories.Add(current))
        {
            stack.Push(current);

            var next = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(next))
            {
                break;
            }

            current = Path.GetFullPath(next);
        }

        while (stack.Count > 0)
        {
            var directory = stack.Pop();
            operations.Add(new PreparedCreateDirectoryOperation(directory, ToRelativePath(directory, root)));
        }
    }

    private static void AddExplicitDirectory(
        string path,
        string root,
        ICollection<PreparedApplyOperation> operations,
        ISet<string> emittedDirectories)
    {
        var fullPath = Path.GetFullPath(path);
        if (!emittedDirectories.Add(fullPath))
        {
            return;
        }

        operations.Add(new PreparedCreateDirectoryOperation(fullPath, ToRelativePath(fullPath, root)));
    }

    private static string ComputeFingerprint(
        string root,
        IReadOnlyList<PreparedApplyOperation> operations,
        IReadOnlyList<GenerationWarning> warnings,
        IReadOnlyList<GenerationConflict> conflicts)
    {
        var builder = new StringBuilder();
        builder.AppendLine(root);

        foreach (var operation in operations)
        {
            builder.Append(operation.Kind).Append('|')
                .Append(operation.Path).Append('|')
                .Append(operation.RelativePath);

            switch (operation)
            {
                case PreparedCreateFileOperation createFile:
                    builder.Append('|').Append(ComputeContentHash(createFile.Content));
                    break;
                case PreparedUpdateFileOperation updateFile:
                    builder.Append('|').Append(ComputeContentHash(updateFile.Content))
                        .Append('|').Append(updateFile.OriginalContentHash ?? "null");
                    break;
                case PreparedDeleteFileOperation deleteFile:
                    builder.Append('|').Append(deleteFile.OriginalContentHash ?? "null");
                    break;
            }

            builder.AppendLine();
        }

        foreach (var warning in warnings)
        {
            builder.Append("warning|").AppendLine(warning.Message);
        }

        foreach (var conflict in conflicts.OrderBy(conflict => conflict.Path, StringComparer.OrdinalIgnoreCase).ThenBy(conflict => conflict.Message, StringComparer.Ordinal))
        {
            builder.Append("conflict|").Append(conflict.Path).Append('|').AppendLine(conflict.Message);
        }

        return ComputeContentHash(builder.ToString());
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || SamePath(fullPath, fullRoot);
    }

    private static string ToRelativePath(string path, string root) =>
        Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/');

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
