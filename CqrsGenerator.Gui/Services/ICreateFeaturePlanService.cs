using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface ICreateFeaturePlanService
{
    GenerationPlan BuildPlan(ProjectWorkspaceContext context, CreateFeatureFormState formState);
}
