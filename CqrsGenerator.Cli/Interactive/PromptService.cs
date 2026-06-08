using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive;

public sealed class PromptService(IAnsiConsole console)
{
    public string? AskFeature(ProjectModel model)
    {
        const string createNew = "+ Создать новую feature";
        const string cancel = "✕ Отмена";

        const string title = "Выберите feature";
        var selected = console.PromptSelection(title,
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(10)
                .EnableSearch()
                .MoreChoicesText("[grey](двигайтесь вверх/вниз, чтобы увидеть остальные варианты)[/]")
                .AddChoices(model.Features.Select(feature => feature.RelativePath).Concat([createNew, cancel])));

        if (selected == cancel)
        {
            return null;
        }

        if (selected != createNew)
        {
            return selected;
        }

        var featurePath = console.Ask<string>("Как назвать feature?");
        CSharpNameValidator.EnsureFeaturePath(featurePath);
        return featurePath;
    }

    public DtoChoice AskDto(ProjectModel model, string featurePath)
    {
        const string createNew = "+ Создать новый DTO";
        const string customType = "+ Ввести тип вручную";
        var featureDtos = model.Dtos
            .Where(dto => dto.FeaturePath == featurePath)
            .ToArray();

        var displayToName = featureDtos.ToDictionary(dto => dto.DisplayName, dto => dto.Name);
        var choices = featureDtos
            .Select(dto => dto.DisplayName)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Concat(["string", "int", "bool", "DateTime", createNew, customType])
            .ToArray();

        const string title = "Какой тип результата?";
        var selected = console.PromptSelection(title,
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(10)
                .AddChoices(choices));

        if (selected == createNew)
        {
            return new DtoChoice("", Create: true);
        }

        if (selected == customType)
        {
            while (true)
            {
                var typeName = console.Ask<string>("Введите тип результата");
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    return new DtoChoice(typeName, Create: false);
                }

                console.MarkupLine("[red]Тип не может быть пустым.[/]");
            }
        }

        return displayToName.TryGetValue(selected, out var dtoName)
            ? new DtoChoice(dtoName, Create: false)
            : new DtoChoice(selected, Create: false);
    }

    public QueryServiceChoice? AskQueryService(GeneratorConfig config, ProjectModel model, string featurePath)
    {
        const string noService = "Без query service, оставить TODO в handler";
        const string createNew = "+ Создать новый query service";
        var serviceChoices = model.QueryServices
            .Where(service => service.FeaturePath == featurePath)
            .Select(service => new
            {
                Label = service.ImplementationPlacement switch
                {
                    QueryServiceImplementationPlacement.Missing => $"{service.InterfaceName} [implementation missing]",
                    QueryServiceImplementationPlacement.Ambiguous => $"{service.InterfaceName} [ambiguous implementation]",
                    _ => service.InterfaceName,
                },
                Service = service,
            })
            .ToArray();
        var choices = serviceChoices
            .Select(choice => choice.Label)
            .Concat([createNew, noService])
            .ToArray();

        const string title = "Query service";
        var selected = console.PromptSelection(title,
            new SelectionPrompt<string>()
                .Title(title)
                .EnableSearch()
                .AddChoices(choices));

        if (selected == noService)
        {
            return null;
        }

        if (selected == createNew)
        {
            var defaultBaseName = GenerationNaming.ToFeatureBaseName(featurePath);
            var interfaceName = console.Prompt(new TextPrompt<string>("Как назвать interface?").DefaultValue(GenerationNaming.GetQueryServiceInterfaceName(defaultBaseName)));
            var implementationName = console.Prompt(new TextPrompt<string>("Как назвать implementation?").DefaultValue(GenerationNaming.GetQueryServiceImplementationName(defaultBaseName)));
            return new QueryServiceChoice(
                interfaceName,
                implementationName,
                InterfacePath: Path.Combine(config.ApplicationFeatureRootPath, featurePath, config.InterfacesFolderName, $"{interfaceName}.cs"),
                ImplementationPath: Path.Combine(config.QueryServicesPath, $"{implementationName}.cs"),
                CreateNew: true);
        }

        var service = serviceChoices.First(choice => choice.Label == selected).Service;
        if (service.ImplementationPlacement is QueryServiceImplementationPlacement.Missing or QueryServiceImplementationPlacement.Ambiguous)
        {
            console.MarkupLine("[yellow]Невозможно автоматически определить implementation для выбранного query service. Нормализуйте размещение implementation вручную или создайте новый service отдельно.[/]");
            return null;
        }

        return new QueryServiceChoice(
            service.InterfaceName,
            service.ImplementationName ?? GenerationNaming.ToDependencyName(service.InterfaceName),
            service.InterfacePath,
            service.ImplementationPath,
            CreateNew: false);
    }

    public ResponseShape AskResponseShape(string typeName)
    {
        const string title = "Форма результата?";
        var selected = console.PromptSelection(title,
            new SelectionPrompt<string>()
                .Title(title)
                .AddChoices(typeName, $"List<{typeName}>", $"IEnumerable<{typeName}>"));

        return selected switch
        {
            var s when s.StartsWith("List<") => ResponseShape.List,
            var s when s.StartsWith("IEnumerable<") => ResponseShape.Enumerable,
            _ => ResponseShape.Single,
        };
    }

    public string? AskCommandResponseType(ProjectModel model, string featurePath)
    {
        const string title = "Command возвращает значение?";
        var selected = console.PromptSelection(title,
            new SelectionPrompt<string>()
                .Title(title)
                .AddChoices("Нет", "Да, примитив", "Да, существующий DTO/тип", "Да, создать новый DTO"));

        return selected switch
        {
            "Нет" => null,
            "Да, примитив" => console.PromptSelection("Какой тип результата?",
                new SelectionPrompt<string>()
                    .Title("Какой тип результата?")
                    .EnableSearch()
                    .AddChoices("int", "bool", "string", "Guid")),
            "Да, существующий DTO/тип" => AskExistingDtoOrType(model, featurePath),
            "Да, создать новый DTO" => AskIdentifier("Как назвать новый DTO?", "CreatedUserDto"),
            _ => null,
        };
    }

    private string AskExistingDtoOrType(ProjectModel model, string featurePath)
    {
        var choice = AskDto(model, featurePath);
        return choice.Create
            ? AskIdentifier("Как назвать новый DTO?", "CreatedUserDto")
            : choice.Name;
    }

    public string AskIdentifier(string prompt, string example)
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

    public string AskIdentifierWithDefault(string prompt, string defaultValue)
    {
        while (true)
        {
            var value = console.Prompt(new TextPrompt<string>(prompt).DefaultValue(defaultValue));
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

    public IReadOnlyList<PropertySpec> AskProperties(string title, bool normalizeName)
    {
        console.MarkupLine(Markup.Escape(title));
        console.MarkupLine("[grey]Примеры: int abonentId, DateTime sinceDate, string Name[/]");

        var properties = new List<PropertySpec>();
        while (true)
        {
            var line = console.Prompt(new TextPrompt<string>(">").AllowEmpty());
            if (string.IsNullOrWhiteSpace(line) || line.Trim() == "-")
            {
                return properties;
            }

            var property = PropertyInputParser.Parse(line);
            if (property is null)
            {
                console.MarkupLine("[red]Введите строку в формате: <тип> <имя>[/]");
                continue;
            }

            properties.Add(normalizeName ? new PropertySpec(property.Type, GenerationNaming.ToPascalCase(property.Name)) : property);
        }
    }
}
