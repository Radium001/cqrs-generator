using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Gui.Services;

public sealed record ProjectLoadResult(
    bool Succeeded,
    string StatusText,
    GeneratorConfig? Config = null,
    ProjectModel? ProjectModel = null,
    string? ErrorMessage = null)
{
    public static ProjectLoadResult Success(string statusText, GeneratorConfig config, ProjectModel projectModel) =>
        new(true, statusText, config, projectModel);

    public static ProjectLoadResult Failure(string statusText, string errorMessage) =>
        new(false, statusText, null, null, errorMessage);
}
