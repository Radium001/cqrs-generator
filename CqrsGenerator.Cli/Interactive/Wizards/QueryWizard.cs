using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class QueryWizard(
    PromptService prompts,
    WizardSectionRenderer sections,
    IAnsiConsole console,
    DtoWizard dtoWizard,
    CoreWorkflowFactory workflows)
{
    private const string HandlerCallService = "Вызов сервиса (простая реализация)";
    private const string HandlerNotImplemented = "Заглушка NotImplementedException";
    private const string SvcMethodFullBody = "Да, с реализацией (Dapper ExecuteAsync)";
    private const string SvcMethodStub = "Да, только заглушка";
    private const string SvcMethodNo = "Нет";

    public GenerationPlan? CreatePlan(
        GeneratorConfig config,
        ProjectModel model,
        QueryServiceChoice? existingService = null,
        string? fixedFeaturePath = null,
        string sectionTitle = "Query Generator",
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

            var queryName = prompts.AskIdentifier("Как назвать query-операцию?", "GetUsers");
            var properties = prompts.AskProperties("Введите параметры query по одному на строку: <тип> <имя>. '-' или пустая строка завершает ввод.", normalizeName: true);
            var dtoSelection = prompts.AskDto(model, featurePath);
            var plan = targetPlan ?? new GenerationPlan();
            var dtoName = dtoSelection.Name;
            if (dtoSelection.Create)
            {
                var dtoResult = dtoWizard.CreateForFeature(config, featurePath, "Query > DTO", updateWebImports: false);
                dtoName = dtoResult.DtoName;
                plan.Merge(dtoResult.Plan);
            }

            var responseShape = prompts.AskResponseShape(dtoName);

            var service = existingService ?? prompts.AskQueryService(config, model, featurePath);
            var generateHandlerBody = service is not null && AskHandlerBodyStyle();
            var serviceWorkflow = service is null
                ? null
                : AskQueryServiceMethodWorkflow(config, featurePath, service, queryName, dtoName, responseShape);
            var sharedDtoPath = Path.Combine(config.ApplicationFeatureRootPath, featurePath, config.DtoFolderName, $"{dtoName}.cs");

            workflows.AddQuery(config)
                .ApplyToPlan(plan, new AddQueryWorkflowRequest(
                    featurePath,
                    queryName,
                    new UseSharedFeatureDtoSelection(
                        dtoName,
                        GenerationNaming.ToDtoNamespace(config, featurePath),
                        sharedDtoPath),
                    responseShape,
                    properties,
                    generateHandlerBody,
                    serviceWorkflow?.GenerateImplementationBody ?? false,
                    serviceWorkflow));

            return plan;
        }
        finally
        {
            sections.End(sectionTitle);
        }
    }

    private bool AskHandlerBodyStyle()
    {
        return console.PromptSelection("Тело handler?",
            new SelectionPrompt<string>()
                .Title("Тело handler?")
                .AddChoices(HandlerCallService, HandlerNotImplemented)) == HandlerCallService;
    }

    private QueryServiceMethodWorkflowRequest AskQueryServiceMethodWorkflow(
        GeneratorConfig config,
        string featurePath,
        QueryServiceChoice service,
        string queryName,
        string dtoName,
        ResponseShape responseShape)
    {
        const string title = "Query Service Generator";
        sections.Begin(title);
        try
        {
            var style = console.PromptSelection("Добавить метод в query service?",
                new SelectionPrompt<string>()
                    .Title("Добавить метод в query service?")
                    .AddChoices(SvcMethodFullBody, SvcMethodStub, SvcMethodNo));

            var generateImplBody = style == SvcMethodFullBody;
            var addMethod = style != SvcMethodNo;
            var methodName = $"{StringUtilities.StripSuffix(queryName, GeneratorConstants.QuerySuffix)}{GeneratorConstants.AsyncSuffix}";
            var returnType = $"Task<{GenerationNaming.GetResponseType(dtoName, responseShape)}>";
            var interfacePath = service.InterfacePath ?? Path.Combine(config.ApplicationFeatureRootPath, featurePath, config.InterfacesFolderName, $"{service.InterfaceName}.cs");
            var implementationPath = service.ImplementationPath ?? Path.Combine(config.QueryServicesPath, $"{service.ImplementationName}.cs");

            return new QueryServiceMethodWorkflowRequest(
                service.InterfaceName,
                service.ImplementationName,
                interfacePath,
                implementationPath,
                service.CreateNew,
                true,
                addMethod,
                generateImplBody,
                methodName,
                returnType,
                dtoName);
        }
        finally
        {
            sections.End(title);
        }
    }
}
