using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Views;

public partial class FilePreviewView : UserControl
{
    private static readonly ISyntaxHighlightingService SyntaxHighlightingService = new SyntaxHighlightingService();
    private ChangedLineBackgroundRenderer? _backgroundRenderer;

    public FilePreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ApplyPreview();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyPreview();
    }

    private void ApplyPreview()
    {
        var preview = DataContext as FilePreviewViewModel;
        Editor.Text = preview?.Content ?? string.Empty;
        Editor.SyntaxHighlighting = SyntaxHighlightingService.GetDefinition(preview?.RelativePath, ActualThemeVariant);

        if (_backgroundRenderer is not null)
        {
            Editor.TextArea.TextView.BackgroundRenderers.Remove(_backgroundRenderer);
            _backgroundRenderer = null;
        }

        if (preview?.ChangedLines.Count > 0)
        {
            var addedBrush = GetThemeBrush("DiffAddedBackgroundBrush");
            var modifiedBrush = GetThemeBrush("DiffModifiedBackgroundBrush");

            if (addedBrush is not null && modifiedBrush is not null)
            {
                _backgroundRenderer = new ChangedLineBackgroundRenderer(preview.ChangedLines, addedBrush, modifiedBrush);
                Editor.TextArea.TextView.BackgroundRenderers.Add(_backgroundRenderer);
            }
        }
    }

    private static IBrush? GetThemeBrush(string key)
    {
        return Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) == true && resource is IBrush brush
            ? brush
            : null;
    }
}
