using CqrsGenerator.Gui.Session.States;

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

        var usages = _session.Relations.FindTargeting(nodeId)
            .Where(edge => !IsOwnershipEdgeFor(edge, node))
            .ToList();
        if (usages.Count > 0)
        {
            return false;
        }

        ClearReferencesToNode(nodeId, includeOwnership: true);
        ClearReferencesFromNode(nodeId);

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

        return true;
    }

    public IReadOnlyList<NodeUsage> FindUsages(Guid nodeId)
    {
        return _session.Relations.FindTargeting(nodeId)
            .Select(edge => new NodeUsage(
                _session.FindNode(edge.SourceNodeId) ?? throw new InvalidOperationException($"Source node {edge.SourceNodeId} not found"),
                edge.Relationship))
            .ToList();
    }

    private bool IsOwnershipEdgeFor(ArtifactReferenceEdge edge, GeneratorNode target)
    {
        return edge.IsOwnership && target.ParentId == edge.SourceNodeId;
    }

    private void ClearReferencesToNode(Guid nodeId, bool includeOwnership)
    {
        foreach (var edge in _session.Relations.FindTargeting(nodeId))
        {
            if (!includeOwnership && edge.IsOwnership)
            {
                continue;
            }

            var source = _session.FindNode(edge.SourceNodeId);
            if (source is not null)
            {
                ClearReference(source, edge.Relationship, nodeId);
            }
        }
    }

    private void ClearReferencesFromNode(Guid nodeId)
    {
        var source = _session.FindNode(nodeId);
        if (source is null)
        {
            return;
        }

        switch (source.State)
        {
            case QueryGeneratorState state:
                state.FeatureRef = null;
                state.ResultDtoRef = null;
                break;
            case DtoGeneratorState state:
                state.FeatureRef = null;
                break;
            case RepositoryGeneratorState state:
                state.FeatureRef = null;
                state.EntityRef = null;
                break;
            case CommandGeneratorState state:
                state.FeatureRef = null;
                state.RepositoryRefs.Clear();
                state.StandardDependencyNames.Clear();
                break;
        }
    }

    private static void ClearReference(GeneratorNode source, string relationship, Guid targetNodeId)
    {
        switch (source.State)
        {
            case QueryGeneratorState state when relationship == "Feature" && state.FeatureRef?.NodeId == targetNodeId:
                state.FeatureRef = null;
                break;
            case QueryGeneratorState state when relationship == "ResultDto" && state.ResultDtoRef?.NodeId == targetNodeId:
                state.ResultDtoRef = null;
                break;
            case DtoGeneratorState state when relationship == "Feature" && state.FeatureRef?.NodeId == targetNodeId:
                state.FeatureRef = null;
                break;
            case RepositoryGeneratorState state when relationship == "Feature" && state.FeatureRef?.NodeId == targetNodeId:
                state.FeatureRef = null;
                break;
            case RepositoryGeneratorState state when relationship == "Entity" && state.EntityRef?.NodeId == targetNodeId:
                state.EntityRef = null;
                break;
            case CommandGeneratorState state when relationship == "Feature" && state.FeatureRef?.NodeId == targetNodeId:
                state.FeatureRef = null;
                break;
            case CommandGeneratorState state when relationship == "Repository":
                RemoveMatching(state.RepositoryRefs, targetNodeId);
                break;
        }
    }

    private static void RemoveMatching(ICollection<ArtifactRef> refs, Guid nodeId)
    {
        foreach (var reference in refs.Where(r => r.NodeId == nodeId).ToList())
        {
            refs.Remove(reference);
        }
    }
}
