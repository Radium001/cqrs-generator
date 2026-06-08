using CqrsGenerator.Core.Workflows;

namespace CqrsGenerator.Gui.Services;

public sealed record AddDtoRequestBuildResult(
    AddDtoWorkflowRequest? Request,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Request is not null && Errors.Count == 0;

    public static AddDtoRequestBuildResult Success(AddDtoWorkflowRequest request) =>
        new(request, []);

    public static AddDtoRequestBuildResult Failure(params string[] errors) =>
        new(null, errors);
}
