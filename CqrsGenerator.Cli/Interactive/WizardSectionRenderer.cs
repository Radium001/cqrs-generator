using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive;

public sealed class WizardSectionRenderer(IAnsiConsole console)
{
    private static readonly (Color Color, string Markup)[] SectionColors =
    [
        (Color.Blue, "blue"),
        (Color.Green, "green"),
        (Color.Orange3, "orange3"),
        (Color.Purple, "purple"),
        (Color.Aqua, "aqua"),
    ];

    private int _indent;

    public void Begin(string title)
    {
        console.WriteLine();
        var (color, _) = SectionColors[_indent % SectionColors.Length];
        var panel = new Panel(new Markup($"[bold]{Markup.Escape(title)}[/]"))
            .BorderColor(color)
            .Expand();
        var (left, right) = GetPaddings();
        console.Write(new Padder(panel, new Padding(left, 0, right, 0)));
        _indent++;
    }

    public void End(string title)
    {
        _indent--;
        var (color, markup) = SectionColors[_indent % SectionColors.Length];
        var rule = new Rule($"[{markup}]{Markup.Escape(title)}[/]")
        {
            Style = new Style(color),
            Justification = Justify.Left,
        };
        var (left, right) = GetPaddings();
        console.Write(new Padder(rule, new Padding(left, 0, right, 0)));
        console.WriteLine();
    }

    private (int Left, int Right) GetPaddings()
    {
        var width = console.Profile.Width;
        if (width <= 0) width = 120;
        var right = (int)(width * 0.15 * (_indent + 1));
        if (right > width * 0.45)
            right = (int)(width * 0.45);
        return (0, right);
    }
}
