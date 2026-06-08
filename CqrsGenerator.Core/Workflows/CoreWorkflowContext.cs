using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;

namespace CqrsGenerator.Core.Workflows;

internal sealed class CoreWorkflowContext
{
    private readonly TemplateProvider _templateProvider;
    private readonly CSharpSyntaxEditor _editor;

    public CoreWorkflowContext(
        GeneratorConfig config,
        TemplateProvider templateProvider,
        CSharpSyntaxEditor editor)
    {
        Config = config;
        _templateProvider = templateProvider;
        _editor = editor;
    }

    public GeneratorConfig Config { get; }

    public CSharpSyntaxEditor Editor => _editor;

    public CreateFeatureWorkflow CreateFeatureWorkflow() => new(this);
    public CreateFeatureBundleWorkflow CreateFeatureBundleWorkflow() => new(this);
    public AddDtoWorkflow CreateAddDtoWorkflow() => new(this);
    public AddQueryWorkflow CreateAddQueryWorkflow() => new(this);
    public AddCommandWorkflow CreateAddCommandWorkflow() => new(this);
    public AddQueryServiceWorkflow CreateAddQueryServiceWorkflow() => new(this);
    public AddRepositoryWorkflow CreateAddRepositoryWorkflow() => new(this);
    public AddRepositoryScenarioWorkflow CreateAddRepositoryScenarioWorkflow() => new(this);
    public AddCommandScenarioWorkflow CreateAddCommandScenarioWorkflow() => new(this);
    public AddEntityWorkflow CreateAddEntityWorkflow() => new(this);
    public AddWebPageWorkflow CreateAddWebPageWorkflow() => new(this);

    public DtoGenerator CreateDtoGenerator() => new(Config, CreateRenderer());
    public LocalQueryDtoGenerator CreateLocalQueryDtoGenerator() => new(Config, CreateRenderer());
    public QueryGenerator CreateQueryGenerator() => new(Config, CreateRenderer());
    public CommandGenerator CreateCommandGenerator() => new(Config, CreateRenderer());
    public QueryServiceGenerator CreateQueryServiceGenerator() => new(Config, CreateRenderer(), _editor);
    public RepositoryGenerator CreateRepositoryGenerator() => new(Config, CreateRenderer(), _editor);
    public EntityGenerator CreateEntityGenerator() => new(Config, CreateRenderer());
    public WebPageGenerator CreateWebPageGenerator() => new(Config, CreateRenderer());
    public FeatureStructureGenerator CreateFeatureStructureGenerator() => new(Config);
    public RazorImportsGenerator CreateRazorImportsGenerator() => new(Config);

    private ScribanTemplateRenderer CreateRenderer() => new(_templateProvider);
}
