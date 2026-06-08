using CqrsGenerator.Core.Validation.Rules;

namespace CqrsGenerator.Core.Validation;

public static class DefaultArchitectureRuleCatalog
{
    private static readonly IReadOnlyList<IArchitectureRule> Rules =
    [
        new SingleQueryServicePerFeatureRule(),
        new QueryServiceImplementationPlacementRule(),
    ];

    public static IReadOnlyList<IArchitectureRule> CreateRules() => Rules;

    public static ArchitectureRuleSet CreateRuleSet() => new(Rules);
}
