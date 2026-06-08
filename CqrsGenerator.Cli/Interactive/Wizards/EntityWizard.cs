using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class EntityWizard(
    PromptService prompts,
    WizardSectionRenderer sections,
    IAnsiConsole console,
    FolderPicker folderPicker,
    CoreWorkflowFactory workflows,
    EfEntityPreparationService efEntityPreparationService)
{
    public GenerationPlan CreatePlan(GeneratorConfig config, ProjectModel? model = null, string? providedName = null, string sectionTitle = "Domain Entity Generator") =>
        Create(config, model, providedName, sectionTitle).Plan;

    public EntityWizardResult Create(GeneratorConfig config, ProjectModel? model = null, string? providedName = null, string sectionTitle = "Domain Entity Generator")
    {
        sections.Begin(sectionTitle);
        try
        {
            string entityName;
            IReadOnlyList<(string DomainName, string EfName)>? efMappings;

            if (providedName is not null)
            {
                entityName = providedName;
                efMappings = null;
            }
            else
            {
                var (efName, mappings) = PickEfEntity(config);
                entityName = prompts.AskIdentifierWithDefault("Как называется domain entity?", efName ?? "HApplication");
                efMappings = mappings;
            }

            var folderChoice = folderPicker.Pick(
                Path.Combine(config.TargetRootPath, "Domain", "Entities"),
                "Выберите папку в Domain/Entities/",
                "Без папки (корень Domain/Entities)");

            string? subFolder;
            if (folderChoice.CreateNew)
            {
                subFolder = folderPicker.AskNewFolderName("Название новой папки", "Application");
            }
            else
            {
                subFolder = folderChoice.RelativePath;
            }

            var entityProps = AskForProperties(config, ref efMappings);
            var createFactory = console.Confirm("Добавить фабричный метод Create?", defaultValue: false);
            var createEfMapping = console.Confirm("Создать EF mapping?", defaultValue: false);

            var plan = workflows.AddEntity(config)
                .CreatePlan(new EntityGenerationRequest
                {
                    EntityName = entityName,
                    SubFolder = subFolder,
                    Properties = entityProps,
                    GenerateFactoryMethod = createFactory,
                    DomainMethods = [],
                    GenerateEfMapping = createEfMapping,
                    EfMappingFields = createEfMapping ? efMappings : null,
                });

            return new EntityWizardResult(entityName, subFolder, plan);
        }
        finally
        {
            sections.End(sectionTitle);
        }
    }

    private (string? EfEntityName, IReadOnlyList<(string DomainName, string EfName)>? Mappings)
        PickEfEntity(GeneratorConfig config)
    {
        var entities = efEntityPreparationService.Discover(config);

        if (entities.Count == 0)
        {
            return (null, null);
        }

        const string skipChoice = "+ Создать заново (без EF Entity)";
        var selected = console.PromptSelection("Какой EF entity?",
            new SelectionPrompt<string>()
                .Title("Создать из существующего EF Entity?")
                .EnableSearch()
                .PageSize(10)
                .AddChoices(entities.Select(entity => entity.Name).Concat([skipChoice])));

        if (selected == skipChoice)
        {
            return (null, null);
        }

        var candidate = entities.First(entity => string.Equals(entity.Name, selected, StringComparison.Ordinal));
        var efProps = candidate.Properties;
        if (efProps.Count == 0)
        {
            console.MarkupLine("[yellow]Не удалось разобрать EF Entity. Свойства нужно будет ввести вручную.[/]");
            return (selected, null);
        }

        var propChoices = efProps.Select(p => $"{p.EfType} {p.EfName}").ToList();
        var selectedProps = console.PromptMultiSelection("Какие свойства добавить?",
            new MultiSelectionPrompt<string>()
                .Title("Какие свойства включить в Domain Entity?")
                .NotRequired()
                .PageSize(15)
                .InstructionsText("[grey]([blue]<space>[/] — выбрать, [green]<enter>[/] — готово)[/]")
                .AddChoices(propChoices));

        if (selectedProps.Count == 0)
        {
            return (selected, null);
        }

        var rename = console.Confirm("Переименовать [grey]IdXxx[/] → [grey]XxxId[/]? (например IdAbonent → AbonentId)", defaultValue: true);
        var selectedIndices = selectedProps
            .Select(choice => propChoices.IndexOf(choice))
            .Where(i => i >= 0)
            .ToList();

        var prepared = efEntityPreparationService.Prepare(
            candidate,
            selectedIndices.Select(index => efProps[index].EfName).ToArray(),
            rename);

        return (selected, prepared.EfMappingFields);
    }

    private IReadOnlyList<PropertySpec> AskForProperties(
        GeneratorConfig config,
        ref IReadOnlyList<(string DomainName, string EfName)>? efMappings)
    {
        if (efMappings is not null && efMappings.Count > 0)
        {
            var props = efMappings.Select(m =>
                new PropertySpec(EfEntityPreparationService.MapEfType(GetEfTypeFromMappings(config, m.DomainName)), m.DomainName))
                .ToList();

            if (console.Confirm("Добавить ещё свойства вручную?", defaultValue: false))
            {
                var extra = prompts.AskProperties("Дополнительные свойства: <тип> <имя>. '-' или пустая строка завершает.", normalizeName: false).ToList();
                props.AddRange(extra);
            }

            return props;
        }

        return prompts.AskProperties("Свойства domain entity: <тип> <имя>. '-' или пустая строка завершает.", normalizeName: false).ToList();
    }

    private string GetEfTypeFromMappings(GeneratorConfig config, string domainName)
    {
        var entitiesDir = Path.Combine(config.TargetRootPath, "Infrastructure", "Data", "Entities");
        foreach (var file in Directory.GetFiles(entitiesDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var efProps = EfEntityParser.Parse(file);
            var match = efProps.FirstOrDefault(p =>
                EfEntityParser.ToDomainName(p.EfName) == domainName || p.EfName == domainName);
            if (match is not null)
            {
                return match.EfType;
            }
        }

        return "string";
    }

}

public sealed record EntityWizardResult(string EntityName, string? SubFolder, GenerationPlan Plan);
