using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddDtoPlanService : IAddDtoPlanService
{
    private readonly CoreWorkflowFactory _workflowFactory;
    private readonly IAddDtoRequestBuilder _requestBuilder;

    public AddDtoPlanService(CoreWorkflowFactory workflowFactory, IAddDtoRequestBuilder requestBuilder)
    {
        _workflowFactory = workflowFactory;
        _requestBuilder = requestBuilder;
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddDtoFormState formState)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestResult = _requestBuilder.Build(formState);
        if (!requestResult.Succeeded || requestResult.Request is null)
        {
            throw new InvalidOperationException(
                requestResult.Errors.FirstOrDefault() ?? "Add DTO request is invalid.");
        }

        return _workflowFactory.AddDto(context.Config).CreatePlan(requestResult.Request);
    }
}
