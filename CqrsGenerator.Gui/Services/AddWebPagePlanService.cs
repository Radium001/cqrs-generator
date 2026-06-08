using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddWebPagePlanService : IAddWebPagePlanService
{
    private readonly CoreWorkflowFactory _workflowFactory;
    private readonly IAddQueryPlanService _queryPlanService;

    public AddWebPagePlanService(
        CoreWorkflowFactory workflowFactory,
        IAddQueryPlanService queryPlanService)
    {
        _workflowFactory = workflowFactory;
        _queryPlanService = queryPlanService;
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddWebPageFormState formState)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(formState);

        var plan = new GenerationPlan();

        foreach (var queryDraft in formState.QueryDrafts)
        {
            plan.Merge(_queryPlanService.BuildPlan(context, queryDraft));
        }

        _workflowFactory.AddWebPage(context.Config).ApplyToPlan(plan, BuildRequest(formState));
        return plan;
    }

    private static WebPageGenerationRequest BuildRequest(AddWebPageFormState formState)
    {
        return new WebPageGenerationRequest
        {
            WebFeaturePath = formState.WebFeaturePath ?? string.Empty,
            PageName = formState.PageName ?? string.Empty,
            Route = formState.Route ?? string.Empty,
            CreateImports = formState.CreateImports,
            Queries = formState.QueryBindings
                .Select(binding => new WebPageQueryBinding(
                    binding.QueryName,
                    null,
                    binding.Args,
                    binding.ResultTypeName,
                    binding.ResponseShape,
                    binding.HasRefresh))
                .ToArray(),
        };
    }
}
