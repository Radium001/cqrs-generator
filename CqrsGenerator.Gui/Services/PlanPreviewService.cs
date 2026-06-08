using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class PlanPreviewService : IPlanPreviewService
{
    private readonly IDiffService _diffService;

    public PlanPreviewService(IDiffService diffService)
    {
        _diffService = diffService;
    }

    public PlanPreviewSnapshot Build(PreparedApplyPackage? package)
    {
        if (package is null)
        {
            return PlanPreviewSnapshot.Empty("Build plan to see affected files.");
        }

        var conflictLookup = package.Conflicts
            .GroupBy(conflict => NormalizeRelativePath(conflict.Path, package.TargetRootPath))
            .ToDictionary(
                group => group.Key,
                group => group.Select(conflict => conflict.Message).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var rootBuilder = new MutableNode(string.Empty, null, PlanTreeNodeKind.Folder);

        foreach (var operation in package.Operations)
        {
            AddOperationNode(rootBuilder, operation, conflictLookup);
        }

        foreach (var conflict in package.Conflicts)
        {
            var relativePath = NormalizeRelativePath(conflict.Path, package.TargetRootPath);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            AddOrphanConflictNode(rootBuilder, relativePath, conflictLookup[relativePath]);
        }

        var roots = rootBuilder.Children.Values
            .Select(BuildNode)
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PlanPreviewSnapshot
        {
            SummaryItems =
            [
                new PreviewSummaryItemViewModel("Create", package.Operations.Count(operation => operation.Kind == GenerationOperationKind.CreateFile).ToString()),
                new PreviewSummaryItemViewModel("Update", package.Operations.Count(operation => operation.Kind == GenerationOperationKind.UpdateFile).ToString()),
                new PreviewSummaryItemViewModel("Delete", package.Operations.Count(operation => operation.Kind == GenerationOperationKind.DeleteFile).ToString()),
                new PreviewSummaryItemViewModel("Warnings", package.Warnings.Count.ToString()),
                new PreviewSummaryItemViewModel("Conflicts", package.Conflicts.Count.ToString()),
            ],
            RootNodes = roots,
            DefaultPreview = roots.FirstOrDefault()?.Preview ?? new FilePreviewViewModel(FilePreviewKind.None, null, "Select a file to preview."),
            EmptyStateText = "Build plan to see affected files.",
            GenerationWarnings = package.Warnings.ToArray(),
            PackageFingerprint = package.Fingerprint,
        };
    }

    private void AddOperationNode(
        MutableNode root,
        PreparedApplyOperation operation,
        IReadOnlyDictionary<string, string[]> conflictLookup)
    {
        var segments = operation.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var isFileOperation = operation.Kind is GenerationOperationKind.CreateFile
            or GenerationOperationKind.UpdateFile
            or GenerationOperationKind.DeleteFile;
        var accumulatedSegments = new List<string>(segments.Length);
        var current = root;

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            accumulatedSegments.Add(segment);
            var currentRelativePath = string.Join('/', accumulatedSegments);
            var isLast = index == segments.Length - 1;
            var nodeKind = isLast && isFileOperation ? PlanTreeNodeKind.File : PlanTreeNodeKind.Folder;

            if (!current.Children.TryGetValue(segment, out var child))
            {
                child = new MutableNode(segment, currentRelativePath, nodeKind);
                current.Children.Add(segment, child);
            }

            if (isLast)
            {
                var conflicts = conflictLookup.TryGetValue(currentRelativePath, out var messages) ? messages : [];
                child.NodeKind = nodeKind;
                child.OperationKind = DetermineOperationKind(operation, conflicts.Length > 0);
                child.Preview = BuildPreview(operation, currentRelativePath, conflicts);
            }

            current = child;
        }
    }

    private void AddOrphanConflictNode(MutableNode root, string relativePath, IReadOnlyList<string> conflicts)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var accumulatedSegments = new List<string>(segments.Length);
        var current = root;

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            accumulatedSegments.Add(segment);
            var currentRelativePath = string.Join('/', accumulatedSegments);
            var isLast = index == segments.Length - 1;
            var nodeKind = isLast ? PlanTreeNodeKind.File : PlanTreeNodeKind.Folder;

            if (!current.Children.TryGetValue(segment, out var child))
            {
                child = new MutableNode(segment, currentRelativePath, nodeKind);
                current.Children.Add(segment, child);
            }

            if (isLast && child.OperationKind == PlanPreviewOperationKind.None)
            {
                child.NodeKind = nodeKind;
                child.OperationKind = PlanPreviewOperationKind.Conflict;
                child.Preview = new FilePreviewViewModel(
                    FilePreviewKind.Conflict,
                    currentRelativePath,
                    string.Join(Environment.NewLine, conflicts),
                    messages: conflicts.ToArray());
            }

            current = child;
        }
    }

    private FilePreviewViewModel BuildPreview(PreparedApplyOperation operation, string relativePath, IReadOnlyList<string> conflicts)
    {
        if (conflicts.Count > 0)
        {
            return new FilePreviewViewModel(
                FilePreviewKind.Conflict,
                relativePath,
                string.Join(Environment.NewLine, conflicts),
                messages: conflicts.ToArray());
        }

        return operation switch
        {
            PreparedCreateFileOperation createFile => new FilePreviewViewModel(
                FilePreviewKind.CreatedFile,
                relativePath,
                createFile.Content),
            PreparedUpdateFileOperation updateFile => new FilePreviewViewModel(
                FilePreviewKind.UpdatedFile,
                relativePath,
                updateFile.Content,
                _diffService.ComputeChangedLines(updateFile.OriginalContent ?? string.Empty, updateFile.Content)),
            PreparedDeleteFileOperation deleteFile => new FilePreviewViewModel(
                FilePreviewKind.DeletedFile,
                relativePath,
                deleteFile.OriginalContent),
            PreparedCreateDirectoryOperation createDirectory => new FilePreviewViewModel(
                FilePreviewKind.Directory,
                relativePath,
                relativePath),
            _ => new FilePreviewViewModel(FilePreviewKind.None, relativePath, null),
        };
    }

    private PlanTreeNodeViewModel BuildNode(MutableNode node)
    {
        var children = node.Children.Values
            .Select(BuildNode)
            .OrderBy(child => child.NodeKind == PlanTreeNodeKind.File ? 1 : 0)
            .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var preview = node.Preview ?? BuildFolderPreview(node.RelativePath, children.Length);

        return new PlanTreeNodeViewModel(
            node.Name,
            node.RelativePath,
            node.NodeKind,
            node.OperationKind,
            children,
            preview);
    }

    private static FilePreviewViewModel BuildFolderPreview(string? relativePath, int childCount)
    {
        var description = string.IsNullOrWhiteSpace(relativePath)
            ? "Folder"
            : $"{relativePath}{Environment.NewLine}{childCount} item(s)";

        return new FilePreviewViewModel(
            FilePreviewKind.Directory,
            relativePath,
            description);
    }

    private static PlanPreviewOperationKind DetermineOperationKind(PreparedApplyOperation operation, bool hasConflict)
    {
        if (hasConflict)
        {
            return PlanPreviewOperationKind.Conflict;
        }

        return operation.Kind switch
        {
            GenerationOperationKind.CreateFile => PlanPreviewOperationKind.CreateFile,
            GenerationOperationKind.UpdateFile => PlanPreviewOperationKind.UpdateFile,
            GenerationOperationKind.DeleteFile => PlanPreviewOperationKind.DeleteFile,
            GenerationOperationKind.CreateDirectory => PlanPreviewOperationKind.CreateDirectory,
            _ => PlanPreviewOperationKind.None,
        };
    }

    private static string NormalizeRelativePath(string path, string? targetRoot)
    {
        var fullPath = Path.GetFullPath(path);
        if (!string.IsNullOrWhiteSpace(targetRoot)
            && fullPath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeSeparators(Path.GetRelativePath(targetRoot, fullPath));
        }

        return NormalizeSeparators(path);
    }

    private static string NormalizeSeparators(string path) => path.Replace('\\', '/');

    private sealed class MutableNode
    {
        public MutableNode(string name, string? relativePath, PlanTreeNodeKind nodeKind)
        {
            Name = name;
            RelativePath = relativePath;
            NodeKind = nodeKind;
        }

        public string Name { get; }

        public string? RelativePath { get; }

        public PlanTreeNodeKind NodeKind { get; set; }

        public PlanPreviewOperationKind OperationKind { get; set; }

        public FilePreviewViewModel? Preview { get; set; }

        public Dictionary<string, MutableNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
