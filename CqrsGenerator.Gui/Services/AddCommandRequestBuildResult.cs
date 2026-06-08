using CqrsGenerator.Core.Workflows;

namespace CqrsGenerator.Gui.Services;

public sealed record AddCommandRequestBuildResult(
    AddCommandScenarioWorkflowRequest? Request,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Request is not null && Errors.Count == 0;

    public static AddCommandRequestBuildResult Success(AddCommandScenarioWorkflowRequest request) => new(request, []);

    public static AddCommandRequestBuildResult Failure(params string[] errors) => new(null, errors);
}
