using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Core.Validation.Rules;

public sealed class QueryServiceImplementationPlacementRule : IArchitectureRule
{
    public string RuleCode => "CQRS-003";

    public string Title => "Query service implementation placement should be unambiguous";

    public IReadOnlyList<ArchitectureWarning> Check(ProjectModel? project, GeneratorConfig? config)
    {
        if (project is null)
        {
            return [];
        }

        var warnings = new List<ArchitectureWarning>();

        foreach (var service in project.QueryServices)
        {
            switch (service.ImplementationPlacement)
            {
                case QueryServiceImplementationPlacement.Ambiguous:
                    warnings.Add(new ArchitectureWarning(
                        RuleCode,
                        "Warning",
                        "Ambiguous query service implementation placement",
                        $"Query service '{service.InterfaceName}' has {service.ImplementationCandidateCount} implementation candidates under QueryServices. Automatic implementation updates are blocked until the project is normalized.",
                        service.FeaturePath));
                    break;
            }
        }

        return warnings;
    }
}
