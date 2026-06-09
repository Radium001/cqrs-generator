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

            foreach (var usage in EnumerateReferences(candidate.State))
            {
                if (usage.Reference.NodeId == nodeId)
                {
                    result.Add(new NodeUsage(candidate, usage.PropertyName));
                }
            }
        }

        return result;
    }

    private static IEnumerable<(ArtifactRef Reference, string PropertyName)> EnumerateReferences(object state)
    {
        switch (state)
        {
            case QueryGeneratorState queryState:
                if (queryState.FeatureRef is not null)
                {
                    yield return (queryState.FeatureRef, nameof(QueryGeneratorState.FeatureRef));
                }

                if (queryState.ResultDtoRef is not null)
                {
                    yield return (queryState.ResultDtoRef, nameof(QueryGeneratorState.ResultDtoRef));
                }

                yield break;

            case CommandGeneratorState commandState:
                if (commandState.FeatureRef is not null)
                {
                    yield return (commandState.FeatureRef, nameof(CommandGeneratorState.FeatureRef));
                }

                foreach (var repositoryRef in commandState.RepositoryRefs)
                {
                    yield return (repositoryRef, nameof(CommandGeneratorState.RepositoryRefs));
                }

                yield break;

            case RepositoryGeneratorState repositoryState:
                if (repositoryState.FeatureRef is not null)
                {
                    yield return (repositoryState.FeatureRef, nameof(RepositoryGeneratorState.FeatureRef));
                }

                if (repositoryState.EntityRef is not null)
                {
                    yield return (repositoryState.EntityRef, nameof(RepositoryGeneratorState.EntityRef));
                }

                yield break;

            case WebPageGeneratorState webPageState:
                if (webPageState.FeatureRef is not null)
                {
                    yield return (webPageState.FeatureRef, nameof(WebPageGeneratorState.FeatureRef));
                }

                foreach (var queryRef in webPageState.QueryRefs)
                {
                    yield return (queryRef, nameof(WebPageGeneratorState.QueryRefs));
                }

                yield break;
        }
    }
}
