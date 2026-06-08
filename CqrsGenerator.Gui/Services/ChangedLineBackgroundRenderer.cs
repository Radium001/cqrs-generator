using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class ChangedLineBackgroundRenderer : IBackgroundRenderer
{
    private readonly HashSet<int> _addedLineNumbers;
    private readonly HashSet<int> _modifiedLineNumbers;
    private readonly IBrush _addedBrush;
    private readonly IBrush _modifiedBrush;

    public ChangedLineBackgroundRenderer(
        IReadOnlyList<ChangedLineInfo> changedLines,
        IBrush addedBrush,
        IBrush modifiedBrush)
    {
        _addedLineNumbers = [];
        _modifiedLineNumbers = [];

        foreach (var line in changedLines)
        {
            var lineNumber = line.LineIndex + 1;
            switch (line.Kind)
            {
                case ChangedLineKind.Added:
                    _addedLineNumbers.Add(lineNumber);
                    break;
                case ChangedLineKind.Modified:
                    _modifiedLineNumbers.Add(lineNumber);
                    break;
            }
        }

        _addedBrush = addedBrush;
        _modifiedBrush = modifiedBrush;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        foreach (var visualLine in textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;

            IBrush? brush;
            if (_addedLineNumbers.Contains(lineNumber))
                brush = _addedBrush;
            else if (_modifiedLineNumbers.Contains(lineNumber))
                brush = _modifiedBrush;
            else
                continue;

            var lineY = visualLine.VisualTop - textView.ScrollOffset.Y;
            var rect = new Rect(0, lineY, textView.Bounds.Width, visualLine.Height);
            drawingContext.DrawRectangle(brush, null, rect);
        }
    }
}
