using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive;

public sealed class InteractiveRunner
{
    private readonly IAnsiConsole _console;

    public InteractiveRunner(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    private const string Exit = "Выход";

    public int Run(string currentDirectory)
    {
        _console.Clear(home: true);

        var targetRoot = TargetRootResolver.Resolve(currentDirectory);
        var config = GeneratorConfig.ForTargetRoot(targetRoot);
        RenderHeader(targetRoot);
        var actions = new InteractiveCompositionRoot(config, _console).BuildActions();
        var actionByLabel = actions.ToDictionary(action => action.Label, StringComparer.Ordinal);

        while (true)
        {
            var model = _console.Status()
                .Start("Сканирую проект...", _ => new ProjectDiscovery(config).Discover());

            var action = _console.PromptSelection("Что создать?",
                new SelectionPrompt<string>()
                    .Title("Что создать?")
                    .AddChoices(actions.Select(menuAction => menuAction.Label).Append(Exit)));

            if (action == Exit)
            {
                return 0;
            }

            _console.Clear(home: true);
            actionByLabel[action].Execute(model);
        }
    }

    private void RenderHeader(string targetRoot)
    {
        var header = new Panel(new Markup($"Целевой проект: [bold]{Markup.Escape(targetRoot)}[/]"))
            .Header("[blue]cqrs-gen[/]")
            .RoundedBorder()
            .BorderColor(Color.Blue);

        _console.Write(Align.Center(header));
        _console.WriteLine();
    }

}
