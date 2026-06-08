using Avalonia.Styling;
using AvaloniaEdit.Highlighting;

namespace CqrsGenerator.Gui.Services;

public interface ISyntaxHighlightingService
{
    IHighlightingDefinition? GetDefinition(string? relativePath, ThemeVariant themeVariant);
}
