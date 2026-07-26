using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Gui.Session;

public sealed class GenerationSessionRelationIndex
{
    private readonly GenerationSession _session;

    public GenerationSessionRelationIndex(GenerationSession session)
    {
        _session = session;
    }

    public IReadOnlyList<ArtifactReferenceEdge> Enumerate()
    {
        return _session.Traverse().SelectMany(GetReferences).ToList();
    }

    public IReadOnlyList<ArtifactReferenceEdge> FindTargeting(Guid targetNodeId)
    {
        return Enumerate()
            .Where(edge => edge.Target.NodeId == targetNodeId)
            .ToList();
    }

    private IEnumerable<ArtifactReferenceEdge> GetReferences(GeneratorNode node)
    {
        foreach (var edge in GetStateReferences(node))
        {
            yield return edge;
        }
    }

    private IEnumerable<ArtifactReferenceEdge> GetStateReferences(GeneratorNode node)
    {
        switch (node.State)
        {
            case QueryGeneratorState state:
                if (state.FeatureRef is not null)
                    yield return new ArtifactReferenceEdge(node.Id, "Feature", state.FeatureRef, IsOwned(node, state.FeatureRef, "Feature"));
                if (state.ResultDtoRef is not null)
                    yield return new ArtifactReferenceEdge(node.Id, "ResultDto", state.ResultDtoRef, IsOwned(node, state.ResultDtoRef, "ResultDto"));
                break;

            case DtoGeneratorState state:
                if (state.FeatureRef is not null)
                    yield return new ArtifactReferenceEdge(node.Id, "Feature", state.FeatureRef, IsOwned(node, state.FeatureRef, "Feature"));
                break;

            case RepositoryGeneratorState state:
                if (state.FeatureRef is not null)
                    yield return new ArtifactReferenceEdge(node.Id, "Feature", state.FeatureRef, IsOwned(node, state.FeatureRef, "Feature"));
                if (state.EntityRef is not null)
                    yield return new ArtifactReferenceEdge(node.Id, "Entity", state.EntityRef, IsOwned(node, state.EntityRef, "Entity"));
                break;

            case CommandGeneratorState state:
                if (state.FeatureRef is not null)
                    yield return new ArtifactReferenceEdge(node.Id, "Feature", state.FeatureRef, IsOwned(node, state.FeatureRef, "Feature"));
                foreach (var reference in state.RepositoryRefs)
                    yield return new ArtifactReferenceEdge(node.Id, "Repository", reference, IsOwned(node, reference, "Repository"));
                break;

        }
    }

    private static bool IsOwned(GeneratorNode source, ArtifactRef reference, string relationship)
    {
        if (reference.NodeId is not Guid targetNodeId)
        {
            return false;
        }

        return source.Children.Any(child =>
            child.Id == targetNodeId &&
            string.Equals(child.RelationshipName, relationship, StringComparison.Ordinal));
    }
}
