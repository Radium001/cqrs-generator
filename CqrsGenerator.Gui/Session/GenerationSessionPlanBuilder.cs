using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session;

public sealed class GenerationSessionPlanBuilder
{
    private static readonly Dictionary<GeneratorNodeKind, int> BuildOrder = new()
    {
        [GeneratorNodeKind.Feature] = 0,
        [GeneratorNodeKind.Entity] = 1,
        [GeneratorNodeKind.Dto] = 2,
        [GeneratorNodeKind.Repository] = 3,
        [GeneratorNodeKind.Query] = 4,
        [GeneratorNodeKind.Command] = 5,
        [GeneratorNodeKind.WebPage] = 6,
    };

    private readonly GeneratorDefinitionCatalog _definitionCatalog;
    private readonly IServiceProvider _serviceProvider;

    public GenerationSessionPlanBuilder(GeneratorDefinitionCatalog definitionCatalog, IServiceProvider serviceProvider)
    {
        _definitionCatalog = definitionCatalog;
        _serviceProvider = serviceProvider;
    }

    public GenerationPlan BuildPlan(
        GenerationSession session,
        CoreWorkflowContext coreContext)
    {
        var result = new GenerationPlan();

        var orderedNodes = session.Traverse()
            .Where(node => node.Status is GeneratorNodeStatus.Valid or GeneratorNodeStatus.Ready)
            .OrderBy(node => GetBuildPriority(node.Kind))
            .ThenBy(node => GetTraversalIndex(session, node))
            .ToList();

        foreach (var node in orderedNodes)
        {
            try
            {
                var definition = _definitionCatalog.GetDefinition(node.Kind);
                var enrichedContext = coreContext with { ServiceProvider = _serviceProvider };
                var nodePlan = definition.BuildPlan(node, session, enrichedContext);

                if (nodePlan.Conflicts.Count > 0)
                {
                    foreach (var conflict in nodePlan.Conflicts)
                    {
                        result.AddConflict(conflict.Path, conflict.Message);
                    }
                }

                result.Merge(nodePlan);
            }
            catch (Exception ex)
            {
                result.AddWarning($"Failed to build plan for '{node.Title}' ({node.Kind}): {ex.Message}");
            }
        }

        return result;
    }

    private static int GetBuildPriority(GeneratorNodeKind kind)
    {
        return BuildOrder.GetValueOrDefault(kind, int.MaxValue);
    }

    private static int GetTraversalIndex(GenerationSession session, GeneratorNode node)
    {
        var index = 0;
        foreach (var candidate in session.Traverse())
        {
            if (candidate.Id == node.Id)
                return index;
            index++;
        }
        return int.MaxValue;
    }
}
