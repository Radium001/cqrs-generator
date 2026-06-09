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
            State = state,
            Title = kind.ToString(),
        };
        _session.Roots.Add(node);
        return node;
    }

    public GeneratorNode CreateChild(GeneratorNode parent, GeneratorNodeKind kind, object state)
    {
        var node = new GeneratorNode
        {
            Kind = kind,
            ParentId = parent.Id,
            State = state,
            Title = kind.ToString(),
        };
        parent.Children.Add(node);
        return node;
    }

    public void OpenNode(Guid nodeId)
    {
        var node = _session.FindNode(nodeId);
        _session.ActiveNode = node;
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

        var usages = FindUsages(nodeId);
        if (usages.Count > 0)
        {
            return false;
        }

        if (node.ParentId is null)
        {
            return _session.Roots.Remove(node);
        }

        var parent = _session.FindNode(node.ParentId.Value);
        if (parent is null)
        {
            return false;
        }

        return parent.Children.Remove(node);
    }

    public IReadOnlyList<NodeUsage> FindUsages(Guid nodeId)
    {
        var result = new List<NodeUsage>();

        foreach (var candidate in _session.Traverse())
        {
            if (candidate.Id == nodeId)
            {
                continue;
            }

            if (candidate.State is CommandGeneratorState cmdState)
            {
                if (cmdState.RepositoryNodeIds.Contains(nodeId))
                {
                    result.Add(new NodeUsage(candidate, nameof(CommandGeneratorState.RepositoryNodeIds)));
                }
            }
            else if (candidate.State is RepositoryGeneratorState repoState)
            {
                if (repoState.EntityNodeId == nodeId)
                {
                    result.Add(new NodeUsage(candidate, nameof(RepositoryGeneratorState.EntityNodeId)));
                }
            }
            else if (candidate.State is QueryGeneratorState queryState)
            {
                if (queryState.ResultDtoNodeId == nodeId)
                {
                    result.Add(new NodeUsage(candidate, nameof(QueryGeneratorState.ResultDtoNodeId)));
                }
            }
            else if (candidate.State is WebPageGeneratorState webState)
            {
                if (webState.QueryNodeIds.Contains(nodeId))
                {
                    result.Add(new NodeUsage(candidate, nameof(WebPageGeneratorState.QueryNodeIds)));
                }
            }
        }

        return result;
    }
}
