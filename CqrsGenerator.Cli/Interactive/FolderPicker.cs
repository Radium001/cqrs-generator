using CqrsGenerator.Core.Validation;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive;

public sealed class FolderPicker(IAnsiConsole console)
{
    private static readonly string[] IgnoredDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".idea",
        "artifacts",
    ];

    public FolderChoice Pick(string rootPath, string promptTitle = "Выберите папку", string? noneLabel = null)
    {
        const string createNew = "+ Создать новую папку";

        var existingFolders = new List<string>();

        if (Directory.Exists(rootPath))
        {
            existingFolders.AddRange(
                Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                    .Where(dir => !IsIgnored(dir))
                    .Select(dir => NormalizeRelativePath(Path.GetRelativePath(rootPath, dir)))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        var choices = new List<string>();
        choices.AddRange(existingFolders);
        choices.Add(createNew);

        if (noneLabel is not null)
        {
            choices.Add(noneLabel);
        }

        var selected = console.PromptSelection(promptTitle,
            new SelectionPrompt<string>()
                .Title(promptTitle)
                .EnableSearch()
                .PageSize(10)
                .AddChoices(choices));

        if (selected == createNew)
        {
            return new FolderChoice(null, CreateNew: true);
        }

        if (selected == noneLabel)
        {
            return new FolderChoice(null, CreateNew: false);
        }

        return new FolderChoice(selected, CreateNew: false);
    }

    public string AskNewFolderName(string prompt = "Название папки", string example = "MyFolder")
    {
        while (true)
        {
            var value = console.Ask<string>($"{prompt} [grey](например {example})[/]");
            try
            {
                CSharpNameValidator.EnsureIdentifier(value, prompt);
                return value;
            }
            catch (ArgumentException exception)
            {
                console.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
            }
        }
    }

    private static bool IsIgnored(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}

public sealed record FolderChoice(string? RelativePath, bool CreateNew);