namespace CqrsGenerator.Gui.Services;

public interface IThemeService
{
    ThemeMode CurrentMode { get; }

    void Apply(ThemeMode mode);
}
