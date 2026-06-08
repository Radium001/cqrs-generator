using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class QueryServiceWizard(
    PromptService prompts,
    WizardSectionRenderer sections,
    IAnsiConsole console,
    CoreWorkflowFactory workflows)
{
    private const string SvcMethodFullBody = "Да, с реализацией (Dapper ExecuteAsync)";
    private const string SvcMethodStub = "Да, только заглушка";
    private const string SvcMethodNo = "Нет";

    public GenerationPlan? CreatePlan(GeneratorConfig config, ProjectModel model, string sectionTitle = "Query Service Generator")
    {
        sections.Begin(sectionTitle);
        try
        {
            var featurePath = prompts.AskFeature(model);
            if (featurePath is null)
            {
                return null;
            }

            return CreatePlanForFeature(config, featurePath);
        }
        finally
        {
            sections.End(sectionTitle);
        }
    }

    public GenerationPlan CreatePlanForFeature(GeneratorConfig config, string featurePath)
    {
        var defaultBaseName = GenerationNaming.ToFeatureBaseName(featurePath);
        var interfaceName = console.Prompt(new TextPrompt<string>("Как назвать query service interface?").DefaultValue(GenerationNaming.GetQueryServiceInterfaceName(defaultBaseName)));
        var implementationName = console.Prompt(new TextPrompt<string>("Как назвать query service implementation?").DefaultValue(GenerationNaming.GetQueryServiceImplementationName(defaultBaseName)));

        var hasMethod = console.PromptSelection("Добавить первый метод?",
            new SelectionPrompt<string>()
                .Title("Добавить первый метод?")
                .AddChoices(SvcMethodFullBody, SvcMethodStub, SvcMethodNo));

        var generateImplBody = hasMethod == SvcMethodFullBody;
        var addMethod = hasMethod != SvcMethodNo;
        string? returnType = null;
        string? methodName = null;
        string? dtoTypeName = null;
        IReadOnlyList<PropertySpec> methodParameters = [];

        if (addMethod)
        {
            returnType = console.PromptSelection("Тип возврата метода?",
                new SelectionPrompt<string>()
                    .Title("Тип возврата метода?")
                    .AddChoices("Task<IEnumerable<Dto>>", "Task<Dto>", "Task"));
            var rawName = prompts.AskIdentifier("Как назвать метод?", "GetDataAsync");
            methodName = rawName.EndsWith(GeneratorConstants.AsyncSuffix, StringComparison.Ordinal) ? rawName : $"{rawName}{GeneratorConstants.AsyncSuffix}";
            methodParameters = prompts.AskProperties("Введите параметры метода: <тип> <имя>. '-' или пустая строка завершает ввод.", normalizeName: true);
            if (generateImplBody)
            {
                dtoTypeName = console.Prompt(new TextPrompt<string>("Название DTO/Model для Dapper?").DefaultValue("YourDto"));
            }
        }

        return workflows.AddQueryService(config)
            .CreateServicePlan(new QueryServiceGenerationRequest
            {
                FeaturePath = featurePath,
                InterfaceName = interfaceName,
                ImplementationName = implementationName,
                ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(config, featurePath, implementationName),
                ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(config, featurePath),
                AddDependencyInjectionRegistration = true,
                InitialReturnType = returnType,
                InitialMethodName = methodName,
                InitialParameters = methodParameters,
                GenerateImplementationBody = generateImplBody,
                DtoTypeName = dtoTypeName,
            });
    }
}
