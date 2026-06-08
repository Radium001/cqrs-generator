using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Workflows;

namespace CqrsGenerator.Cli.Interactive;

public sealed class CoreWorkflowFactory
{
    private readonly CqrsGenerator.Core.Workflows.CoreWorkflowFactory _inner = new();

    public CreateFeatureWorkflow CreateFeature(GeneratorConfig config) =>
        _inner.CreateFeature(config);

    public CreateFeatureBundleWorkflow CreateFeatureBundle(GeneratorConfig config) =>
        _inner.CreateFeatureBundle(config);

    public AddDtoWorkflow AddDto(GeneratorConfig config) =>
        _inner.AddDto(config);

    public AddQueryWorkflow AddQuery(GeneratorConfig config) =>
        _inner.AddQuery(config);

    public AddCommandWorkflow AddCommand(GeneratorConfig config) =>
        _inner.AddCommand(config);

    public AddQueryServiceWorkflow AddQueryService(GeneratorConfig config) =>
        _inner.AddQueryService(config);

    public AddRepositoryWorkflow AddRepository(GeneratorConfig config) =>
        _inner.AddRepository(config);

    public AddEntityWorkflow AddEntity(GeneratorConfig config) =>
        _inner.AddEntity(config);

    public AddWebPageWorkflow AddWebPage(GeneratorConfig config) =>
        _inner.AddWebPage(config);
}
