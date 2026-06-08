using CqrsGenerator.Core.Workflows;

namespace CqrsGenerator.Gui.Services;

public sealed record AddRepositoryRequestBuildResult(
    AddRepositoryScenarioWorkflowRequest? Request,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Request is not null && Errors.Count == 0;

    public static AddRepositoryRequestBuildResult Success(AddRepositoryScenarioWorkflowRequest request) => new(request, []);

    public static AddRepositoryRequestBuildResult Failure(params string[] errors) => new(null, errors);
}
