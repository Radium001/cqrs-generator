using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Gui.Session;

public sealed class GenerationSessionRelationshipService
{
    public bool CommitChild(GeneratorNode parent, GeneratorNode child, GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(child.RelationshipName))
        {
            return false;
        }

        var reference = session.Artifacts.FindByNodeId(child.Id)?.Ref;
        if (reference is null)
        {
            return false;
        }

        switch (parent.State)
        {
            case QueryGeneratorState queryState when string.Equals(child.RelationshipName, "Feature", StringComparison.Ordinal):
                queryState.FeatureRef = reference;
                return true;

            case QueryGeneratorState queryState when string.Equals(child.RelationshipName, "ResultDto", StringComparison.Ordinal):
                queryState.ResultDtoRef = reference;
                return true;

            case RepositoryGeneratorState repositoryState when string.Equals(child.RelationshipName, "Entity", StringComparison.Ordinal):
                repositoryState.EntityRef = reference;
                return true;

            case CommandGeneratorState commandState when string.Equals(child.RelationshipName, "Repository", StringComparison.Ordinal):
                if (!commandState.RepositoryRefs.Any(existing => ArtifactRefEquals(existing, reference)))
                {
                    commandState.RepositoryRefs.Add(reference);
                }

                return true;

            case WebPageGeneratorState webPageState when string.Equals(child.RelationshipName, "Query", StringComparison.Ordinal):
                if (!webPageState.QueryRefs.Any(existing => ArtifactRefEquals(existing, reference)))
                {
                    webPageState.QueryRefs.Add(reference);
                }

                return true;
        }

        return false;
    }

    private static bool ArtifactRefEquals(ArtifactRef left, ArtifactRef right)
    {
        if (left.NodeId.HasValue && right.NodeId.HasValue)
        {
            return left.NodeId == right.NodeId;
        }

        return left.Kind == right.Kind
               && left.Origin == right.Origin
               && string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
               && string.Equals(left.FeaturePath, right.FeaturePath, StringComparison.OrdinalIgnoreCase);
    }
}
