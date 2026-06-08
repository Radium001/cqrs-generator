using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class CreateFeaturePlanService : ICreateFeaturePlanService
{
    private readonly CoreWorkflowFactory _workflowFactory;

    public CreateFeaturePlanService(CoreWorkflowFactory workflowFactory)
    {
        _workflowFactory = workflowFactory;
    }

    public GenerationPlan BuildPlan(ProjectWorkspaceContext context, CreateFeatureFormState formState)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(formState);

        var featureName = formState.FeatureName?.Trim();
        if (string.IsNullOrWhiteSpace(featureName))
            throw new InvalidOperationException("Feature name is required.");

        var featurePath = string.IsNullOrWhiteSpace(formState.Subfolder)
            ? featureName
            : $"{formState.Subfolder}/{featureName}";

        var config = context.Config;
        var workflow = _workflowFactory.CreateFeature(config);
        var request = new FeatureStructureGenerationRequest
        {
            FeaturePath = featurePath,
            CreateWebFeature = formState.CreateWebFeature,
        };

        return workflow.CreatePlan(request);
    }
}
