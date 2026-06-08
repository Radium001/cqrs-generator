using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class FeatureWizard(
    WizardSectionRenderer sections,
    IAnsiConsole console,
    QueryWizard queryWizard,
    CommandWizard commandWizard,
    CoreWorkflowFactory workflows)
{
    public GenerationPlan? CreatePlan(GeneratorConfig config, ProjectModel model)
    {
        const string title = "Feature Generator";
        sections.Begin(title);
        try
        {
            var featurePath = console.Ask<string>("Как назвать feature?");
            try
            {
                CSharpNameValidator.EnsureFeaturePath(featurePath);
            }
            catch (ArgumentException exception)
            {
                console.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
                return null;
            }

            var artifacts = console.PromptMultiSelection("Что создать сейчас?",
                new MultiSelectionPrompt<string>()
                    .Title("Что создать сейчас?")
                    .NotRequired()
                    .AddChoices("Query service", "Query", "Command"));

            var plan = new GenerationPlan();
            var createQueryService = artifacts.Contains("Query service", StringComparer.Ordinal);
            string? queryServiceInterface = null;
            string? queryServiceImplementation = null;

            if (createQueryService)
            {
                var defaultBaseName = GenerationNaming.ToFeatureBaseName(featurePath);
                queryServiceInterface = console.Prompt(new TextPrompt<string>("Как назвать query service interface?").DefaultValue(GenerationNaming.GetQueryServiceInterfaceName(defaultBaseName)));
                queryServiceImplementation = console.Prompt(new TextPrompt<string>("Как назвать query service implementation?").DefaultValue(GenerationNaming.GetQueryServiceImplementationName(defaultBaseName)));
            }

            var queryServiceRequest = createQueryService
                ? new QueryServiceGenerationRequest
                {
                    FeaturePath = featurePath,
                    InterfaceName = queryServiceInterface!,
                    ImplementationName = queryServiceImplementation!,
                    ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(config, featurePath, queryServiceImplementation!),
                    ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(config, featurePath),
                    AddDependencyInjectionRegistration = true,
                }
                : null;

            if (createQueryService)
            {
                const string queryServiceTitle = "Feature > Query Service";
                sections.Begin(queryServiceTitle);
                try
                {
                    plan.Merge(workflows.CreateFeatureBundle(config)
                        .CreatePlan(new CreateFeatureBundleWorkflowRequest(featurePath, queryServiceRequest)));
                }
                finally
                {
                    sections.End(queryServiceTitle);
                }
            }
            else
            {
                plan.Merge(workflows.CreateFeatureBundle(config)
                    .CreatePlan(new CreateFeatureBundleWorkflowRequest(featurePath)));
            }

            var featureModel = AddFeatureToModel(model, featurePath);
            var existingService = createQueryService
                ? new QueryServiceChoice(
                    queryServiceInterface!,
                    queryServiceImplementation!,
                    InterfacePath: Path.Combine(config.ApplicationFeatureRootPath, featurePath, config.InterfacesFolderName, $"{queryServiceInterface}.cs"),
                    ImplementationPath: GenerationNaming.GetQueryServiceImplementationPath(config, featurePath, queryServiceImplementation!),
                    CreateNew: false)
                : null;

            if (artifacts.Contains("Query", StringComparer.Ordinal))
            {
                var first = true;
                while (true)
                {
                    var label = first ? "Создать query" : "Добавить ещё query?";
                    if (!console.Confirm(label, defaultValue: true))
                    {
                        break;
                    }

                    first = false;
                    var queryPlan = queryWizard.CreatePlan(
                        config,
                        featureModel,
                        existingService,
                        fixedFeaturePath: featurePath,
                        sectionTitle: "Feature > Query",
                        targetPlan: plan);
                    if (queryPlan is not null && !ReferenceEquals(queryPlan, plan))
                    {
                        plan.Merge(queryPlan);
                    }

                    if (existingService is not null)
                    {
                        existingService = existingService with { CreateNew = false };
                    }
                }
            }

            if (artifacts.Contains("Command", StringComparer.Ordinal))
            {
                var first = true;
                while (true)
                {
                    var label = first ? "Создать command" : "Добавить ещё command?";
                    if (!console.Confirm(label, defaultValue: true))
                    {
                        break;
                    }

                    first = false;
                    var commandPlan = commandWizard.CreatePlan(
                        config,
                        featureModel,
                        fixedFeaturePath: featurePath,
                        sectionTitle: "Feature > Command",
                        targetPlan: plan);
                    if (commandPlan is not null && !ReferenceEquals(commandPlan, plan))
                    {
                        plan.Merge(commandPlan);
                    }
                }
            }

            return plan;
        }
        finally
        {
            sections.End(title);
        }
    }

    private static ProjectModel AddFeatureToModel(ProjectModel model, string featurePath)
    {
        if (model.Features.Any(feature => feature.RelativePath == featurePath))
        {
            return model;
        }

        var feature = new FeatureInfo(
            featurePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? featurePath,
            featurePath,
            Path.Combine(model.Paths.ApplicationFeatures, featurePath));

        return new ProjectModel
        {
            Paths = model.Paths,
            Features = model.Features.Concat([feature]).ToArray(),
            Dtos = model.Dtos,
            Queries = model.Queries,
            Commands = model.Commands,
            QueryServices = model.QueryServices,
            Repositories = model.Repositories,
            Entities = model.Entities,
            WebFeatures = model.WebFeatures,
            DependencyInjection = model.DependencyInjection,
        };
    }
}
