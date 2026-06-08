using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddCommandPlanService : IAddCommandPlanService
{
    private readonly CoreWorkflowFactory _workflowFactory;
    private readonly IAddCommandRequestBuilder _requestBuilder;

    public AddCommandPlanService(CoreWorkflowFactory workflowFactory, IAddCommandRequestBuilder requestBuilder)
    {
        _workflowFactory = workflowFactory;
        _requestBuilder = requestBuilder;
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddCommandFormState formState)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestResult = _requestBuilder.Build(formState);
        if (!requestResult.Succeeded || requestResult.Request is null)
        {
            throw new InvalidOperationException(requestResult.Errors.FirstOrDefault() ?? "Add Command request is invalid.");
        }

        return _workflowFactory.AddCommandScenario(context.Config).CreatePlan(requestResult.Request);
    }
}
