using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class CommandWizard(
    PromptService prompts,
    WizardSectionRenderer sections,
    IAnsiConsole console,
    RepositoryWizard repositoryWizard,
    CoreWorkflowFactory workflows)
{
    public GenerationPlan? CreatePlan(
        GeneratorConfig config,
        ProjectModel model,
        string? fixedFeaturePath = null,
        string sectionTitle = "Command Generator",
        GenerationPlan? targetPlan = null)
    {
        sections.Begin(sectionTitle);
        try
        {
            var featurePath = fixedFeaturePath ?? prompts.AskFeature(model);
            if (featurePath is null)
            {
                return null;
            }

            var commandName = prompts.AskIdentifier("Как назвать command-операцию?", "CreateUser");
            var properties = prompts.AskProperties("Введите параметры command по одному на строку: <тип> <имя>. '-' или пустая строка завершает ввод.", normalizeName: true);
            var responseType = prompts.AskCommandResponseType(model, featurePath);

            var plan = targetPlan ?? new GenerationPlan();
            var dependencies = AskCommandDependencies(config, model, plan);

            workflows.AddCommand(config)
                .ApplyToPlan(plan, new AddCommandWorkflowRequest(
                    featurePath,
                    commandName,
                    responseType,
                    properties,
                    dependencies));

            return plan;
        }
        finally
        {
            sections.End(sectionTitle);
        }
    }

    private IReadOnlyList<CommandHandlerDependency> AskCommandDependencies(GeneratorConfig config, ProjectModel model, GenerationPlan plan)
    {
        var deps = new List<CommandHandlerDependency>();
        var createdRepos = new List<string>();
        var selectedItems = new HashSet<string> { "ICurrentUserContext", "IUnitOfWork" };

        while (true)
        {
            var repoItems = model.Repositories
                .Select(r => r.InterfaceName)
                .Concat(createdRepos)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var choices = new List<string>();
            choices.AddRange(repoItems);
            choices.Add("ICurrentUserContext");
            choices.Add("IUnitOfWork");
            choices.Add("+ Создать репозиторий");
            choices.Add("+ Ввести вручную");

            var prompt = new MultiSelectionPrompt<string>()
                .Title("Выберите зависимости handler")
                .NotRequired()
                .PageSize(10)
                .InstructionsText("[grey]([blue]<space>[/] — выбрать, [green]<enter>[/] — готово)[/]")
                .AddChoices(choices);

            foreach (var item in selectedItems.Intersect(choices))
            {
                prompt.Select(item);
            }

            var selected = console.PromptMultiSelection("Выберите зависимости handler", prompt);

            selectedItems.Clear();
            foreach (var item in selected)
            {
                if (item is not ("+ Создать репозиторий" or "+ Ввести вручную"))
                    selectedItems.Add(item);
            }

            if (selected.Contains("+ Создать репозиторий"))
            {
                var result = repositoryWizard.CreatePlan(config, model, "Command > Repository");
                if (result is null)
                {
                    continue;
                }

                plan.Merge(result.Plan);

                var repoInterface = GenerationNaming.GetRepositoryInterfaceName(result.EntityName);
                createdRepos.Add(repoInterface);
                selectedItems.Add(repoInterface);
                deps.Add(new CommandHandlerDependency(repoInterface, GenerationNaming.ToDependencyName(repoInterface)));
                continue;
            }

            if (selected.Contains("+ Ввести вручную"))
            {
                var typeName = console.Ask<string>("Введите интерфейс зависимости (например IMyService)");
                if (typeName.Length < 2 || typeName[0] != 'I' || !char.IsUpper(typeName[1]))
                {
                    console.MarkupLine("[red]Имя интерфейса должно начинаться с I и заглавной буквы (например IMyService).[/]");
                    continue;
                }

                try
                {
                    CSharpNameValidator.EnsureIdentifier(typeName, "тип зависимости");
                    selectedItems.Add(typeName);
                    deps.Add(new CommandHandlerDependency(typeName, GenerationNaming.ToDependencyName(typeName)));
                }
                catch (ArgumentException exception)
                {
                    console.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
                }

                continue;
            }

            selected = selected
                .Where(item => item != "+ Создать репозиторий" && item != "+ Ввести вручную")
                .Where(item => !deps.Any(d => d.Type == item))
                .ToList();

            foreach (var item in selected)
            {
                deps.Add(new CommandHandlerDependency(item, GenerationNaming.ToDependencyName(item)));
            }

            return deps;
        }
    }
}
