using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddDtoPlanService
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddDtoFormState formState);
}
