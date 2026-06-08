using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddRepositoryPlanService
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddRepositoryFormState formState);
}
