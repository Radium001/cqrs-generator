namespace CqrsGenerator.Core.Generation;

public sealed class FileSystemPlanApplier
{
    public void Apply(GenerationPlan plan, bool force = false)
    {
        if (!force && plan.HasConflicts)
        {
            var conflicts = string.Join(
                Environment.NewLine,
                plan.Conflicts.Select(conflict => $" - {conflict.Path}: {conflict.Message}"));
            throw new InvalidOperationException($"Generation stopped because the plan has conflicts:{Environment.NewLine}{conflicts}");
        }

        foreach (var operation in plan.Operations)
        {
            switch (operation)
            {
                case CreateDirectoryOperation createDirectory:
                    Directory.CreateDirectory(createDirectory.Path);
                    break;
                case CreateFileOperation createFile:
                    WriteFile(createFile.Path, createFile.Content);
                    break;
                case UpdateFileOperation updateFile:
                    WriteFile(updateFile.Path, updateFile.Content);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported generation operation: {operation.GetType().Name}");
            }
        }
    }

    private static void WriteFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }
}
