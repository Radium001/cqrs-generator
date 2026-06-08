using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class FeatureStructureGenerator(GeneratorConfig config)
{
    public GenerationPlan CreatePlan(FeatureStructureGenerationRequest request)
    {
        Validate(request);

        var plan = new GenerationPlan();
        var featurePath = StringUtilities.NormalizeFeaturePath(request.FeaturePath);
        var featureRoot = Path.Combine(config.ApplicationFeatureRootPath, featurePath);

        if (Directory.Exists(featureRoot))
        {
            plan.AddConflict(featureRoot, "Feature уже существует.");
        }

        plan.AddDirectory(featureRoot);
        plan.AddDirectory(Path.Combine(featureRoot, config.QueriesFolderName));
        plan.AddDirectory(Path.Combine(featureRoot, config.CommandsFolderName));
        plan.AddDirectory(Path.Combine(featureRoot, config.InterfacesFolderName));

        if (request.CreateWebFeature)
        {
            plan.AddDirectory(Path.Combine(config.WebFeatureRootPath, featurePath));
        }

        return plan;
    }

    private static void Validate(FeatureStructureGenerationRequest request)
    {
        CSharpNameValidator.EnsureFeaturePath(request.FeaturePath);
    }
}
