using CqrsGenerator.Gui.ViewModels;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace CqrsGenerator.Gui.Services;

public sealed class DiffService : IDiffService
{
    public IReadOnlyList<ChangedLineInfo> ComputeChangedLines(string oldContent, string newContent)
    {
        var diff = InlineDiffBuilder.Diff(oldContent, newContent);
        var result = new List<ChangedLineInfo>();
        var newLineIndex = 0;

        foreach (var line in diff.Lines)
        {
            switch (line.Type)
            {
                case ChangeType.Inserted:
                    result.Add(new ChangedLineInfo(newLineIndex, ChangedLineKind.Added));
                    newLineIndex++;
                    break;
                case ChangeType.Modified:
                    result.Add(new ChangedLineInfo(newLineIndex, ChangedLineKind.Modified));
                    newLineIndex++;
                    break;
                case ChangeType.Unchanged:
                    newLineIndex++;
                    break;
                case ChangeType.Deleted:
                    break;
            }
        }

        return result;
    }
}
