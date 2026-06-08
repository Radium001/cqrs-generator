using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddEntityPlanService : IAddEntityPlanService
{
    private readonly CoreWorkflowFactory _workflowFactory;
    private readonly IAddEntityRequestBuilder _requestBuilder;

    public AddEntityPlanService(CoreWorkflowFactory workflowFactory, IAddEntityRequestBuilder requestBuilder)
    {
        _workflowFactory = workflowFactory;
        _requestBuilder = requestBuilder;
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddEntityFormState formState)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestResult = _requestBuilder.Build(formState);
        if (!requestResult.Succeeded || requestResult.Request is null)
        {
            throw new InvalidOperationException(requestResult.Errors.FirstOrDefault() ?? "Add Entity request is invalid.");
        }

        return _workflowFactory.AddEntity(context.Config).CreatePlan(requestResult.Request);
    }
}
