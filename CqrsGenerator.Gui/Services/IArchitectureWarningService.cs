using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Gui.Services;

public interface IArchitectureWarningService
{
    IReadOnlyList<ArchitectureWarning> GetWarnings(ProjectWorkspaceContext context);
}
