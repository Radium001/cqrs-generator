using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Core.Validation;

public sealed class ArchitectureRuleSet
{
    private readonly IReadOnlyList<IArchitectureRule> _rules;

    public ArchitectureRuleSet(IEnumerable<IArchitectureRule> rules)
    {
        _rules = rules.ToList();
    }

    public IReadOnlyList<ArchitectureWarning> CheckAll(ProjectModel? project, GeneratorConfig? config)
    {
        return _rules.SelectMany(rule => rule.Check(project, config)).ToList();
    }
}
