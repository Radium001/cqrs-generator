using CqrsGenerator.Gui.Models;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Services;

public interface IAddWebPagePlanService
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddWebPageFormState formState);
}
