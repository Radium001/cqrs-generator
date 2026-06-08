using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddQueryPlanService : IAddQueryPlanService
{
    private readonly CoreWorkflowFactory _workflowFactory;
    private readonly IAddQueryRequestBuilder _requestBuilder;

    public AddQueryPlanService(CoreWorkflowFactory workflowFactory, IAddQueryRequestBuilder requestBuilder)
    {
        _workflowFactory = workflowFactory;
        _requestBuilder = requestBuilder;
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddQueryFormState formState)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestResult = _requestBuilder.Build(formState);
        if (!requestResult.Succeeded || requestResult.Request is null)
        {
            throw new InvalidOperationException(requestResult.Errors.FirstOrDefault() ?? "Add Query request is invalid.");
        }

        return _workflowFactory.AddQuery(context.Config).CreatePlan(requestResult.Request);
    }
}
