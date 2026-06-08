using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddRepositoryPlanService : IAddRepositoryPlanService
{
    private readonly CoreWorkflowFactory _workflowFactory;
    private readonly IAddRepositoryRequestBuilder _requestBuilder;

    public AddRepositoryPlanService(CoreWorkflowFactory workflowFactory, IAddRepositoryRequestBuilder requestBuilder)
    {
        _workflowFactory = workflowFactory;
        _requestBuilder = requestBuilder;
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddRepositoryFormState formState)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestResult = _requestBuilder.Build(formState);
        if (!requestResult.Succeeded || requestResult.Request is null)
        {
            throw new InvalidOperationException(requestResult.Errors.FirstOrDefault() ?? "Add Repository request is invalid.");
        }

        return _workflowFactory.AddRepositoryScenario(context.Config).CreatePlan(requestResult.Request);
    }
}
