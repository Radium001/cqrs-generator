using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IDiffService
{
    IReadOnlyList<ChangedLineInfo> ComputeChangedLines(string oldContent, string newContent);
}
