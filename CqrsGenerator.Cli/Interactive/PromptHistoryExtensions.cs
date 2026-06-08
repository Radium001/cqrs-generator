using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive;

public static class PromptHistoryExtensions
{
    public static T PromptSelection<T>(this IAnsiConsole console, string question, SelectionPrompt<T> prompt)
        where T : notnull
    {
        var selected = console.Prompt(prompt);
        console.MarkupLine($"{Markup.Escape(question)} [cyan]{Markup.Escape(FormatValue(selected))}[/]");
        return selected;
    }

    public static IReadOnlyList<T> PromptMultiSelection<T>(this IAnsiConsole console, string question, MultiSelectionPrompt<T> prompt)
        where T : notnull
    {
        var selected = console.Prompt(prompt);
        var text = selected.Count == 0
            ? "ничего"
            : string.Join(", ", selected.Select(FormatValue));

        console.MarkupLine($"{Markup.Escape(question)} [cyan]{Markup.Escape(text)}[/]");
        return selected;
    }

    private static string FormatValue<T>(T value) =>
        value?.ToString() ?? "";
}
