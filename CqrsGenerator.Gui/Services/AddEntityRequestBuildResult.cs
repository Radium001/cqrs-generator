using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Services;

public sealed record AddEntityRequestBuildResult(
    EntityGenerationRequest? Request,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Request is not null && Errors.Count == 0;

    public static AddEntityRequestBuildResult Success(EntityGenerationRequest request) => new(request, []);

    public static AddEntityRequestBuildResult Failure(params string[] errors) => new(null, errors);
}
