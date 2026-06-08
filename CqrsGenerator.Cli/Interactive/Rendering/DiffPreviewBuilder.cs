using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Rendering;

public sealed record DiffPreview(string Markup, int ChangedLineCount);

public static class DiffPreviewBuilder
{
    private const int ContextLines = 3;
    private const int MaxRenderedLines = 120;

    public static DiffPreview? Build(string oldContent, string newContent)
    {
        var diff = InlineDiffBuilder.Diff(oldContent, newContent);
        var interesting = diff.Lines
            .Select((line, index) => new { line, index })
            .Where(item => item.line.Type is ChangeType.Inserted or ChangeType.Deleted or ChangeType.Modified)
            .Select(item => item.index)
            .ToArray();

        if (interesting.Length == 0)
        {
            return null;
        }

        var included = CollectContextLines(diff, interesting);
        var lines = new List<string>();
        var changedLineCount = 0;
        var previous = -1;

        foreach (var index in included.Take(MaxRenderedLines))
        {
            if (previous >= 0 && index > previous + 1)
            {
                lines.Add("[grey]  ...[/]");
            }

            var line = diff.Lines[index];
            if (line.Type is ChangeType.Inserted or ChangeType.Deleted)
            {
                changedLineCount++;
            }

            lines.Add(RenderDiffLine(line));
            previous = index;
        }

        if (included.Count > MaxRenderedLines)
        {
            lines.Add($"[grey]  ... {included.Count - MaxRenderedLines} more diff lines hidden[/]");
        }

        return new DiffPreview(string.Join("\n", lines), changedLineCount);
    }

    private static SortedSet<int> CollectContextLines(DiffPaneModel diff, IReadOnlyList<int> interesting)
    {
        var included = new SortedSet<int>();
        foreach (var index in interesting)
        {
            var start = Math.Max(0, index - ContextLines);
            var end = Math.Min(diff.Lines.Count - 1, index + ContextLines);
            for (var i = start; i <= end; i++)
            {
                included.Add(i);
            }
        }

        return included;
    }

    private static string RenderDiffLine(DiffPiece line)
    {
        var text = Markup.Escape(line.Text ?? "");
        return line.Type switch
        {
            ChangeType.Inserted => $"[green]+ {text}[/]",
            ChangeType.Deleted => $"[red]- {text}[/]",
            ChangeType.Modified => $"[orange3]~ {text}[/]",
            _ => $"[grey]  {text}[/]",
        };
    }
}
