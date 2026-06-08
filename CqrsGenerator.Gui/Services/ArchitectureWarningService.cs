using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Gui.Services;

public sealed class ArchitectureWarningService : IArchitectureWarningService
{
    private readonly ArchitectureRuleSet _ruleSet;

    public ArchitectureWarningService()
        : this(DefaultArchitectureRuleCatalog.CreateRuleSet())
    {
    }

    public ArchitectureWarningService(ArchitectureRuleSet ruleSet)
    {
        _ruleSet = ruleSet;
    }

    public IReadOnlyList<ArchitectureWarning> GetWarnings(ProjectWorkspaceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _ruleSet.CheckAll(context.ProjectModel, context.Config);
    }
}
