using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive;

public sealed class InteractivePlanExecutor(IAnsiConsole console)
{
    public void Run(GeneratorConfig config, GenerationPlan? plan)
    {
        if (plan is null)
        {
            return;
        }

        var renderer = new ConsolePlanRenderer(config.TargetRootPath, console);
        renderer.Render(plan);

        if (plan.HasConflicts)
        {
            renderer.RenderConflicts(plan);

            var choice = console.PromptSelection("Конфликтующие файлы:",
                new SelectionPrompt<string>()
                    .AddChoices("Применить с заменой", "Отмена"));

            if (choice == "Отмена") return;
        }

        while (true)
        {
            var next = console.PromptSelection("Что дальше?",
                new SelectionPrompt<string>()
                    .Title("Что дальше?")
                    .AddChoices("Применить", "Посмотреть код", "Отмена"));

            if (next == "Отмена")
            {
                return;
            }

            if (next == "Посмотреть код")
            {
                renderer.RenderCodeView(plan);
                continue;
            }

            console.Status()
                .Start("Применяю план...", _ => new FileSystemPlanApplier().Apply(plan, plan.HasConflicts));
            return;
        }
    }
}
