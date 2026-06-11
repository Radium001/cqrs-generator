using System.Linq;

namespace CqrsGenerator.Gui.Session;

public sealed class GenerationSessionNavigator : IGenerationSessionNavigator
{
    private readonly GenerationSession _session;

    public GenerationSessionNavigator(GenerationSession session)
    {
        _session = session;
    }

    public GeneratorNode CreateRoot(GeneratorNodeKind kind, object state)
    {
        var node = new GeneratorNode
        {
            Kind = kind,
            Lifecycle = GeneratorNodeLifecycle.Committed,
            State = state,
            Title = kind.ToString(),
        };
        _session.Roots.Add(node);
        return node;
    }

    public GeneratorNode CreateChild(GeneratorNode parent, GeneratorNodeKind kind, object state, string? relationshipName = null)
    {
        var node = new GeneratorNode
        {
            Kind = kind,
            Lifecycle = GeneratorNodeLifecycle.Draft,
            ParentId = parent.Id,
            RelationshipName = relationshipName,
            State = state,
            Title = kind.ToString(),
        };
        parent.Children.Add(node);
        return node;
    }

    public bool OpenNode(Guid nodeId)
    {
        var node = _session.FindNode(nodeId);
        _session.ActiveNode = node;
        return node is not null;
    }

    public void OpenParent()
    {
        if (_session.ActiveNode?.ParentId is null)
        {
            return;
        }

        var parent = _session.FindNode(_session.ActiveNode.ParentId.Value);
        _session.ActiveNode = parent;
    }

    public bool RemoveNode(Guid nodeId)
    {
        var node = _session.FindNode(nodeId);
        if (node is null)
        {
            return false;
        }

        var usages = _session.References.FindTargeting(nodeId);
        if (usages.Count > 0)
        {
            return false;
        }

        if (node.ParentId is null)
        {
            if (!_session.Roots.Remove(node))
                return false;
        }
        else
        {
            var parent = _session.FindNode(node.ParentId.Value);
            if (parent is null || !parent.Children.Remove(node))
                return false;
        }

        _session.References.ClearAllForSource(nodeId);
        return true;
    }

    public IReadOnlyList<NodeUsage> FindUsages(Guid nodeId)
    {
        return _session.References.FindTargeting(nodeId)
            .Select(entry => new NodeUsage(
                _session.FindNode(entry.SourceId) ?? throw new InvalidOperationException($"Source node {entry.SourceId} not found"),
                entry.Relationship))
            .ToList();
    }
}
