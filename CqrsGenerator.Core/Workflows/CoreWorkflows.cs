using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Workflows;

public sealed class CreateFeatureWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal CreateFeatureWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(FeatureStructureGenerationRequest request) =>
        _context.CreateFeatureStructureGenerator().CreatePlan(request);
}

public sealed class AddQueryServiceWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddQueryServiceWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreateServicePlan(QueryServiceGenerationRequest request) =>
        _context.CreateQueryServiceGenerator().CreateServicePlan(request);

    public GenerationPlan AddMethodPlan(QueryServiceMethodGenerationRequest request) =>
        _context.CreateQueryServiceGenerator().AddMethodPlan(request);
}

public sealed class AddRepositoryWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddRepositoryWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(RepositoryGenerationRequest request) =>
        _context.CreateRepositoryGenerator().CreatePlan(request);
}

public sealed class AddEntityWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddEntityWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(EntityGenerationRequest request) =>
        _context.CreateEntityGenerator().CreatePlan(request);
}

public sealed class AddWebPageWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddWebPageWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(WebPageGenerationRequest request) =>
        _context.CreateWebPageGenerator().CreatePlan(request);

    public void ApplyToPlan(GenerationPlan plan, WebPageGenerationRequest request) =>
        _context.CreateWebPageGenerator().ApplyToPlan(plan, request);
}
