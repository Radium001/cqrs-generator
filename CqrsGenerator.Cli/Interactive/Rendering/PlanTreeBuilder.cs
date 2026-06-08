using CqrsGenerator.Core.Generation;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CqrsGenerator.Cli.Interactive.Rendering;

public sealed class PlanTreeBuilder(string targetRoot)
{
    public Tree Build(
        IReadOnlyList<GenerationOperation> operations,
        Func<GenerationOperation, string, IRenderable?> buildFileNode)
    {
        var tree = new Tree(Markup.Escape(Path.GetFileName(targetRoot)));
        var dirNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);

        var firstLevelDirs = operations
            .Select(op => NormalizeSeparators(Path.GetRelativePath(targetRoot, op.Path)))
            .Select(relative => relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Where(seg => seg.Length > 0)
            .Select(seg => seg[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var dir in firstLevelDirs)
        {
            var label = $"[white]{Markup.Escape(dir)}[/]";
            dirNodes[dir] = tree.AddNode(label);
        }

        foreach (var operation in operations)
        {
            AddOperation(tree, dirNodes, operation, buildFileNode);
        }

        return tree;
    }

    private void AddOperation(
        Tree tree,
        Dictionary<string, TreeNode> dirNodes,
        GenerationOperation operation,
        Func<GenerationOperation, string, IRenderable?> buildFileNode)
    {
        var relative = NormalizeSeparators(Path.GetRelativePath(targetRoot, operation.Path));
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var isFile = operation.Kind is GenerationOperationKind.CreateFile
                  or GenerationOperationKind.UpdateFile;
        var accumulatedPath = "";

        for (var i = 0; i < segments.Length; i++)
        {
            accumulatedPath = i == 0 ? segments[i] : $"{accumulatedPath}/{segments[i]}";
            var isLast = i == segments.Length - 1;
            var isFileLeaf = isLast && isFile;

            if (!isFileLeaf && dirNodes.ContainsKey(accumulatedPath))
            {
                continue;
            }

            if (isFileLeaf)
            {
                var fileNode = buildFileNode(operation, segments[i]);
                if (fileNode is not null)
                {
                    AddChild(tree, dirNodes, segments, i, fileNode);
                }
            }
            else
            {
                var color = i == 0 ? "white" : "grey";
                var label = $"[{color}]{Markup.Escape(segments[i])}[/]";
                dirNodes[accumulatedPath] = AddChild(tree, dirNodes, segments, i, label);
            }
        }
    }

    private static TreeNode AddChild(
        Tree tree,
        IReadOnlyDictionary<string, TreeNode> dirNodes,
        string[] segments,
        int index,
        string label)
    {
        if (index == 0)
        {
            return tree.AddNode(label);
        }

        var parentPath = string.Join('/', segments[..index]);
        return dirNodes[parentPath].AddNode(label);
    }

    private static TreeNode AddChild(
        Tree tree,
        IReadOnlyDictionary<string, TreeNode> dirNodes,
        string[] segments,
        int index,
        IRenderable renderable)
    {
        if (index == 0)
        {
            return tree.AddNode(renderable);
        }

        var parentPath = string.Join('/', segments[..index]);
        return dirNodes[parentPath].AddNode(renderable);
    }

    private static string NormalizeSeparators(string path) =>
        path.Replace('\\', '/');
}
