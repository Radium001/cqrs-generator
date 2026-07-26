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
                PropagateQueryFeatureToOwnedDto(parent, queryState.FeatureRef);
                return true;

            case QueryGeneratorState queryState when string.Equals(child.RelationshipName, "ResultDto", StringComparison.Ordinal):
                queryState.ResultDtoRef = reference;
                if (child.State is DtoGeneratorState dtoState)
                {
                    dtoState.FeatureRef = queryState.FeatureRef;
                }
                return true;

            case RepositoryGeneratorState repositoryState when string.Equals(child.RelationshipName, "Entity", StringComparison.Ordinal):
                repositoryState.EntityRef = reference;
                return true;

            case CommandGeneratorState commandState when string.Equals(child.RelationshipName, "Feature", StringComparison.Ordinal):
                commandState.FeatureRef = reference;
                return true;

            case CommandGeneratorState commandState when string.Equals(child.RelationshipName, "Repository", StringComparison.Ordinal):
                if (!commandState.RepositoryRefs.Any(existing => ArtifactKey.Equals(existing, reference)))
                {
                    commandState.RepositoryRefs.Add(reference);
                }
                return true;

        }

        return false;
    }

    private static void PropagateQueryFeatureToOwnedDto(GeneratorNode queryNode, ArtifactRef? featureRef)
    {
        var ownedDto = queryNode.Children.FirstOrDefault(child =>
            child.Kind == GeneratorNodeKind.Dto &&
            string.Equals(child.RelationshipName, "ResultDto", StringComparison.Ordinal));

        if (ownedDto?.State is DtoGeneratorState dtoState)
        {
            dtoState.FeatureRef = featureRef;
        }
    }
}
