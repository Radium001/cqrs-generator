using CqrsGenerator.Cli.Interactive.Rendering;
using CqrsGenerator.Core.Generation;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CqrsGenerator.Cli.Interactive;

public sealed class ConsolePlanRenderer(string targetRoot, IAnsiConsole? console = null)
{
    private readonly IAnsiConsole _console = console ?? AnsiConsole.Console;
    private readonly PlanTreeBuilder _treeBuilder = new(targetRoot);

    public void Render(GenerationPlan plan)
    {
        if (plan.Warnings.Count > 0)
        {
            foreach (var warning in plan.Warnings)
            {
                _console.MarkupLine($"[yellow]Предупреждение:[/] {Markup.Escape(warning.Message)}");
            }

            _console.WriteLine();
        }

        if (plan.Conflicts.Count > 0)
        {
            var conflictTable = new Table().Title("Конфликты").AddColumn("Путь").AddColumn("Причина");
            foreach (var conflict in plan.Conflicts)
            {
                conflictTable.AddRow(Relative(conflict.Path), Markup.Escape(conflict.Message));
            }

            _console.Write(conflictTable);
            return;
        }

        var tree = _treeBuilder.Build(plan.Operations, BuildSummaryFileNode);
        var legend = new Markup($"  [green]✓ создание[/]   [orange3]~ модификация[/]   [grey]▸ директория[/]");
        var content = new Rows(tree, new Rule(), legend);
        var panel = new Panel(content)
            .Header("План изменений")
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .Expand();

        _console.Write(Align.Center(panel));
    }

    private static string Relative(string path) =>
        Markup.Escape(path);

    public void RenderCodeView(GenerationPlan plan)
    {
        var tree = _treeBuilder.Build(plan.Operations, BuildCodePanel);
        var legend = new Markup($"  [green]✓ создание[/]   [orange3]~ модификация[/]");
        var content = new Rows(tree, new Rule(), legend);
        var panel = new Panel(content)
            .Header("План изменений — просмотр кода")
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .Expand();

        _console.Write(Align.Center(panel));
    }

    private static Markup? BuildSummaryFileNode(GenerationOperation operation, string fileName)
    {
        if (operation.Kind is not (GenerationOperationKind.CreateFile or GenerationOperationKind.UpdateFile))
        {
            return null;
        }

        var color = operation.Kind == GenerationOperationKind.CreateFile ? "green" : "orange3";
        return new Markup($"[{color}]{Markup.Escape(fileName)}[/]");
    }

    private static Panel? BuildCodePanel(GenerationOperation op, string fileName)
    {
        if (op is CreateFileOperation cfo)
        {
            return BuildFullPanel(cfo.Content, fileName, Color.Green, "");
        }

        if (op is UpdateFileOperation ufo)
        {
            return BuildDiffPanel(ufo, fileName);
        }

        return null;
    }

    private static Panel? BuildFullPanel(string content, string fileName, Color borderColor, string label)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            var highlighted = CSharpHighlighter.Highlight(content);
            return new Panel(new Markup(highlighted))
                .Header($"{fileName}{label}")
                .BorderColor(borderColor)
                .Expand();
        }
        catch
        {
            return new Panel(Markup.Escape(content))
                .Header($"{fileName}{label}")
                .BorderColor(borderColor)
                .Expand();
        }
    }

    private static Panel? BuildDiffPanel(UpdateFileOperation operation, string fileName)
    {
        var oldContent = operation.OriginalContent
                         ?? (File.Exists(operation.Path) ? File.ReadAllText(operation.Path) : null);
        return BuildDiffPanel(fileName, operation.Content, oldContent);
    }

    private static Panel? BuildDiffPanel(string fileName, string newContent, string? oldContent)
    {
        if (string.IsNullOrWhiteSpace(newContent) || oldContent is null)
            return BuildFullPanel(newContent, fileName, Color.Orange3, " (modified)");

        var preview = DiffPreviewBuilder.Build(oldContent, newContent);

        if (preview is null)
            return new Panel(new Markup($"[grey]{Markup.Escape(fileName)} — без изменений[/]"))
                .BorderColor(Color.Grey)
                .Expand();

        if (string.IsNullOrWhiteSpace(preview.Markup))
            return BuildFullPanel(newContent, fileName, Color.Orange3, " (modified)");

        return new Panel(new Markup(preview.Markup))
            .Header($"{fileName} ({preview.ChangedLineCount} changed line{(preview.ChangedLineCount == 1 ? "" : "s")})")
            .BorderColor(Color.Orange3)
            .Expand();
    }

    public void RenderConflicts(GenerationPlan plan)
    {
        var panels = new List<IRenderable>();

        foreach (var conflict in plan.Conflicts)
        {
            var op = plan.Operations.FirstOrDefault(o => o.Path == conflict.Path);
            if (op is null) continue;

            var fileName = Path.GetFileName(op.Path);
            Panel? diffPanel = null;

            if (op is CreateFileOperation cfo)
            {
                try
                {
                    if (File.Exists(cfo.Path))
                    {
                        var existing = File.ReadAllText(cfo.Path);
                        diffPanel = BuildDiffPanel(fileName, cfo.Content, existing);
                    }
                    else
                    {
                        diffPanel = BuildFullPanel(cfo.Content, fileName, Color.Red, " (файл удалён)");
                    }
                }
                catch
                {
                    diffPanel = BuildFullPanel(cfo.Content, fileName, Color.Red, " (ошибка чтения)");
                }
            }
            else if (op is UpdateFileOperation ufo)
            {
                diffPanel = BuildDiffPanel(fileName, ufo.Content, ufo.OriginalContent);
            }

            if (diffPanel is not null)
                panels.Add(diffPanel);
        }

        if (panels.Count == 0)
        {
            _console.MarkupLine("[yellow]Нет конфликтующих файлов для просмотра.[/]");
            return;
        }

        var content = new Rows(panels);
        var panel = new Panel(content)
            .Header("[red]Конфликтующие файлы[/]")
            .RoundedBorder()
            .BorderColor(Color.Red)
            .Expand();

        _console.Write(Align.Left(panel));
    }
}
