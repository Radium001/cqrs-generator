using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class ThemeOptionViewModel
{
    public ThemeOptionViewModel(ThemeMode mode, string title)
    {
        Mode = mode;
        Title = title;
    }

    public ThemeMode Mode { get; }

    public string Title { get; }
}
