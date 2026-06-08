using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class RepositoryWizard(
    PromptService prompts,
    WizardSectionRenderer sections,
    IAnsiConsole console,
    EntityWizard entityWizard,
    CoreWorkflowFactory workflows)
{
    public RepositoryWizardResult? CreatePlan(GeneratorConfig config, ProjectModel model, string sectionTitle = "Repository Generator")
    {
        sections.Begin(sectionTitle);
        try
        {
            var plan = new GenerationPlan();
            var (entityName, entityNamespace) = ResolveEntity(config, model, plan, "Repository > Domain Entity");
            plan.Merge(CreateRepositoryOnlyPlan(config, entityName, entityNamespace));
            return new RepositoryWizardResult(entityName, plan);
        }
        finally
        {
            sections.End(sectionTitle);
        }
    }

    private (string EntityName, string EntityNamespace) ResolveEntity(GeneratorConfig config, ProjectModel model, GenerationPlan plan, string entitySectionTitle)
    {
        if (model.Entities.Count > 0)
        {
            const string createNew = "+ Создать новую domain entity";
            var selected = console.PromptSelection("Для какой domain entity создать репозиторий?",
                new SelectionPrompt<string>()
                    .Title("Для какой domain entity создать репозиторий?")
                    .EnableSearch()
                    .AddChoices(model.Entities.Select(e => e.DisplayName).Concat([createNew])));

            if (selected != createNew)
            {
                var entity = model.Entities.First(e => e.DisplayName == selected);
                var entityNamespace = GetEntityNamespace(entity.Path, config.TargetRootPath);
                return (entity.Name, entityNamespace);
            }
        }

        var result = entityWizard.Create(config, model, sectionTitle: entitySectionTitle);
        plan.Merge(result.Plan);
        var ns = result.SubFolder is null ? GeneratorConstants.DomainEntitiesNamespace : $"{GeneratorConstants.DomainEntitiesNamespace}.{result.SubFolder}";
        return (result.EntityName, ns);
    }

    private static string GetEntityNamespace(string entityPath, string targetRoot)
    {
        var entitiesDir = Path.Combine(targetRoot, "Domain", "Entities");
        var dir = Path.GetDirectoryName(entityPath);
        if (dir is not null && dir.StartsWith(entitiesDir, StringComparison.OrdinalIgnoreCase) && dir.Length > entitiesDir.Length)
        {
            var sub = dir[(entitiesDir.Length + 1)..];
            return $"{GeneratorConstants.DomainEntitiesNamespace}.{sub}";
        }

        return GeneratorConstants.DomainEntitiesNamespace;
    }

    private GenerationPlan CreateRepositoryOnlyPlan(GeneratorConfig config, string entityName, string entityNamespace)
    {
        return BuildRepositoryPlan(config, entityName, entityNamespace);
    }

    private GenerationPlan BuildRepositoryPlan(GeneratorConfig config, string entityName, string entityNamespace)
    {
        var methodChoices = new List<string> { GeneratorConstants.RepoMethodGetById, GeneratorConstants.RepoMethodAdd, GeneratorConstants.RepoMethodUpdate, GeneratorConstants.RepoMethodDelete, "+ Добавить свой метод" };
        var selectedMethods = console.PromptMultiSelection("Какие методы добавить?", 
            new MultiSelectionPrompt<string>()
                .Title("Какие методы добавить в репозиторий?")
                .NotRequired()
                .PageSize(10)
                .Select(GeneratorConstants.RepoMethodGetById)
                .Select(GeneratorConstants.RepoMethodAdd)
                .Select(GeneratorConstants.RepoMethodUpdate)
                .InstructionsText("[grey]([blue]<space>[/] — выбрать, [green]<enter>[/] — готово)[/]")
                .AddChoices(methodChoices));

        var repoMethods = new List<RepositoryMethodSpec>();
        if (selectedMethods.Contains(GeneratorConstants.RepoMethodGetById))
            repoMethods.Add(new(GeneratorConstants.RepoMethodGetById, $"Task<{entityName}>", [new("int", GeneratorConstants.DefaultIdParamName)]));
        if (selectedMethods.Contains(GeneratorConstants.RepoMethodAdd))
            repoMethods.Add(new(GeneratorConstants.RepoMethodAdd, "Task", [new(entityName, GeneratorConstants.DefaultEntityParamName)]));
        if (selectedMethods.Contains(GeneratorConstants.RepoMethodUpdate))
            repoMethods.Add(new(GeneratorConstants.RepoMethodUpdate, "Task", [new(entityName, GeneratorConstants.DefaultEntityParamName)]));
        if (selectedMethods.Contains(GeneratorConstants.RepoMethodDelete))
            repoMethods.Add(new(GeneratorConstants.RepoMethodDelete, "Task", [new("int", GeneratorConstants.DefaultIdParamName)]));

        if (selectedMethods.Contains("+ Добавить свой метод"))
        {
            while (true)
            {
                var customName = prompts.AskIdentifier("Имя метода (пусто — закончить)?", "GetActiveAsync");
                if (string.IsNullOrWhiteSpace(customName))
                {
                    break;
                }

                var returnType = console.Ask<string>($"Тип возврата (например Task<{entityName}>)");
                var customParams = prompts.AskProperties("Параметры: <тип> <имя>. '-' или пустая строка завершает.", normalizeName: true);
                repoMethods.Add(new(customName, returnType, customParams));
            }
        }

        return workflows.AddRepository(config)
            .CreatePlan(new RepositoryGenerationRequest
            {
                EntityName = entityName,
                EntityNamespace = entityNamespace,
                AddDependencyInjectionRegistration = true,
                Methods = repoMethods,
            });
    }
}

public sealed record RepositoryWizardResult(string EntityName, GenerationPlan Plan);
