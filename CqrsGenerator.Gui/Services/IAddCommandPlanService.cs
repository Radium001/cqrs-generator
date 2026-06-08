using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddCommandPlanService
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddCommandFormState formState);
}
