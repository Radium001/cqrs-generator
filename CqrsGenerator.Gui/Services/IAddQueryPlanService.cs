using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddQueryPlanService
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddQueryFormState formState);
}
