using Avalonia;
using Avalonia.Styling;

namespace CqrsGenerator.Gui.Services;

public sealed class ThemeService : IThemeService
{
    private readonly Application _application;

    public ThemeService(Application application)
    {
        _application = application;
    }

    public ThemeMode CurrentMode =>
        _application.RequestedThemeVariant switch
        {
            { } variant when variant == ThemeVariant.Light => ThemeMode.Light,
            { } variant when variant == ThemeVariant.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };

    public void Apply(ThemeMode mode)
    {
        _application.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
