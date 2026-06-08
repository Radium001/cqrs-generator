using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Core.Validation;

public interface IArchitectureRule
{
    string RuleCode { get; }

    string Title { get; }

    IReadOnlyList<ArchitectureWarning> Check(ProjectModel? project, GeneratorConfig? config);
}
