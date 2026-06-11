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

        var treeOrder = session.Traverse()
            .Select((node, index) => new { node.Id, index })
            .ToDictionary(x => x.Id, x => x.index);

        var orderedNodes = session.Traverse()
            .Where(node => node.Lifecycle == GeneratorNodeLifecycle.Committed)
            .Where(node => node.Status is GeneratorNodeStatus.Valid or GeneratorNodeStatus.Ready)
            .Where(node => ShouldBuildAsPlanNode(session, node))
            .OrderBy(node => GetBuildPriority(node.Kind))
            .ThenBy(node => treeOrder.GetValueOrDefault(node.Id))
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

    private static bool ShouldBuildAsPlanNode(GenerationSession session, GeneratorNode node)
    {
        if (node.ParentId is null)
        {
            return true;
        }

        var parent = session.FindNode(node.ParentId.Value);
        if (parent?.Kind == GeneratorNodeKind.Query &&
            node.Kind == GeneratorNodeKind.Dto &&
            string.Equals(node.RelationshipName, "ResultDto", StringComparison.Ordinal))
        {
            // Query-owned result DTOs are inline settings for AddQueryWorkflow's
            // CreateLocalQueryDtoSelection. Building them as standalone DTO nodes
            // would incorrectly place them in the shared DTOs folder.
            return false;
        }

        return true;
    }

    private static int GetBuildPriority(GeneratorNodeKind kind)
    {
        return BuildOrder.GetValueOrDefault(kind, int.MaxValue);
    }
}
