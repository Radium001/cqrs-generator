using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddEntityPlanService
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddEntityFormState formState);
}
