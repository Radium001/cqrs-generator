using CqrsGenerator.Core.Workflows;

namespace CqrsGenerator.Gui.Services;

public sealed record AddQueryRequestBuildResult(
    AddQueryWorkflowRequest? Request,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Request is not null && Errors.Count == 0;

    public static AddQueryRequestBuildResult Success(AddQueryWorkflowRequest request) => new(request, []);

    public static AddQueryRequestBuildResult Failure(params string[] errors) => new(null, errors);
}
