using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Workflows;

public sealed record CreateFeatureBundleWorkflowRequest(
    string FeaturePath,
    QueryServiceGenerationRequest? QueryService = null,
    IReadOnlyList<AddDtoWorkflowRequest>? Dtos = null,
    IReadOnlyList<AddQueryWorkflowRequest>? Queries = null,
    IReadOnlyList<AddCommandScenarioWorkflowRequest>? Commands = null,
    EntityGenerationRequest? Entity = null,
    AddRepositoryScenarioWorkflowRequest? Repository = null,
    WebPageGenerationRequest? WebPage = null);

public sealed class CreateFeatureBundleWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal CreateFeatureBundleWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(CreateFeatureBundleWorkflowRequest request)
    {
        var plan = new GenerationPlan();
        ApplyToPlan(plan, request);
        return plan;
    }

    public void ApplyToPlan(GenerationPlan plan, CreateFeatureBundleWorkflowRequest request)
    {
        plan.Merge(_context.CreateFeatureWorkflow().CreatePlan(new FeatureStructureGenerationRequest
        {
            FeaturePath = request.FeaturePath,
        }));

        if (request.Dtos is not null)
        {
            foreach (var dto in request.Dtos)
            {
                plan.Merge(_context.CreateAddDtoWorkflow().CreatePlan(dto));
            }
        }

        if (request.Queries is not null)
        {
            foreach (var query in request.Queries)
            {
                _context.CreateAddQueryWorkflow().ApplyToPlan(plan, query);
            }
        }

        if (request.Commands is not null)
        {
            foreach (var command in request.Commands)
            {
                _context.CreateAddCommandScenarioWorkflow().ApplyToPlan(plan, command);
            }
        }

        if (request.Entity is not null)
        {
            plan.Merge(_context.CreateAddEntityWorkflow().CreatePlan(request.Entity));
        }

        if (request.Repository is not null)
        {
            _context.CreateAddRepositoryScenarioWorkflow().ApplyToPlan(plan, request.Repository);
        }

        if (request.QueryService is not null)
        {
            plan.Merge(_context.CreateAddQueryServiceWorkflow()
                .CreateServicePlan(request.QueryService));
        }

        if (request.WebPage is not null)
        {
            _context.CreateAddWebPageWorkflow().ApplyToPlan(plan, request.WebPage);
        }
    }
}
