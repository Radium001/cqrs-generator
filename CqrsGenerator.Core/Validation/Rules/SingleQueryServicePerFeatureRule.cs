using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Core.Validation.Rules;

public sealed class SingleQueryServicePerFeatureRule : IArchitectureRule
{
    public string RuleCode => "CQRS-001";

    public string Title => "Feature should have at most one query service";

    public IReadOnlyList<ArchitectureWarning> Check(ProjectModel? project, GeneratorConfig? config)
    {
        if (project is null)
            return [];

        var warnings = new List<ArchitectureWarning>();

        foreach (var feature in project.Features)
        {
            var count = project.QueryServices.Count(s => s.FeaturePath == feature.RelativePath);
            if (count > 1)
            {
                warnings.Add(new ArchitectureWarning(
                    RuleCode,
                    "Warning",
                    Title,
                    $"Feature '{feature.Name}' has {count} query services. Expected at most 1.",
                    feature.RelativePath));
            }
        }

        return warnings;
    }
}
