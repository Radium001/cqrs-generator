using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Templates;

namespace CqrsGenerator.Core.Workflows;

public sealed class CoreWorkflowFactory
{
    private readonly TemplateProvider _templateProvider = new();
    private readonly CSharpSyntaxEditor _editor = new();

    public CreateFeatureWorkflow CreateFeature(GeneratorConfig config) =>
        CreateContext(config).CreateFeatureWorkflow();

    public CreateFeatureBundleWorkflow CreateFeatureBundle(GeneratorConfig config) =>
        CreateContext(config).CreateFeatureBundleWorkflow();

    public AddDtoWorkflow AddDto(GeneratorConfig config) =>
        CreateContext(config).CreateAddDtoWorkflow();

    public AddQueryWorkflow AddQuery(GeneratorConfig config) =>
        CreateContext(config).CreateAddQueryWorkflow();

    public AddCommandWorkflow AddCommand(GeneratorConfig config) =>
        CreateContext(config).CreateAddCommandWorkflow();

    public AddQueryServiceWorkflow AddQueryService(GeneratorConfig config) =>
        CreateContext(config).CreateAddQueryServiceWorkflow();

    public AddRepositoryWorkflow AddRepository(GeneratorConfig config) =>
        CreateContext(config).CreateAddRepositoryWorkflow();

    public AddRepositoryScenarioWorkflow AddRepositoryScenario(GeneratorConfig config) =>
        CreateContext(config).CreateAddRepositoryScenarioWorkflow();

    public AddCommandScenarioWorkflow AddCommandScenario(GeneratorConfig config) =>
        CreateContext(config).CreateAddCommandScenarioWorkflow();

    public AddEntityWorkflow AddEntity(GeneratorConfig config) =>
        CreateContext(config).CreateAddEntityWorkflow();

    private CoreWorkflowContext CreateContext(GeneratorConfig config) =>
        new(config, _templateProvider, _editor);
}
