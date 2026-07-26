using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Gui.Session;

public sealed class SessionArtifactIndex
{
    private static readonly IReadOnlyList<string> DtoSuffixes = ["", GeneratorConstants.DtoSuffix];

    private readonly GenerationSession _session;
    private ProjectModel? _projectModel;

    public SessionArtifactIndex(GenerationSession session)
    {
        _session = session;
    }

    public void SetProjectModel(ProjectModel? projectModel)
    {
        _projectModel = projectModel;
    }

    public ProjectModel? ProjectModel => _projectModel;

    public IReadOnlyList<AvailableArtifactItem> GetFeatures()
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var feature in _projectModel.Features)
            {
                var reference = new ArtifactRef(
                    GeneratorNodeKind.Feature,
                    ArtifactOrigin.Project,
                    feature.Name,
                    FeaturePath: feature.RelativePath,
                    ProjectPath: feature.Path,
                    DisplayName: feature.Name);

                if (seen.Add(CreateFeatureKey(reference)))
                {
                    result.Add(CreateItem(reference));
                }
            }
        }

        foreach (var reference in EnumerateSessionArtifacts(GeneratorNodeKind.Feature))
        {
            if (seen.Add(CreateFeatureKey(reference)))
            {
                result.Add(CreateItem(reference));
            }
        }

        return result;
    }

    public IReadOnlyList<AvailableArtifactItem> GetDtos(string? featurePath = null)
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var dto in _projectModel.Dtos)
            {
                if (!MatchesFeaturePath(dto.FeaturePath, featurePath))
                {
                    continue;
                }

                var reference = new ArtifactRef(
                    GeneratorNodeKind.Dto,
                    ArtifactOrigin.Project,
                    dto.Name,
                    FeaturePath: dto.FeaturePath,
                    ProjectPath: dto.Path,
                    Namespace: dto.Namespace,
                    OwnerName: dto.OwnerQueryName,
                    DisplayName: string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.Name : dto.DisplayName);

                if (seen.Add(CreateDtoKey(reference)))
                {
                    result.Add(CreateItem(reference));
                }
            }
        }

        foreach (var reference in EnumerateSessionArtifacts(GeneratorNodeKind.Dto).Where(reference => MatchesFeaturePath(reference.FeaturePath, featurePath)))
        {
            if (seen.Add(CreateDtoKey(reference)))
            {
                result.Add(CreateItem(reference));
            }
        }

        return result;
    }

    public IReadOnlyList<AvailableArtifactItem> GetEntities(string? featurePath = null)
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var entity in _projectModel.Entities)
            {
                if (!MatchesFeaturePath(entity.RelativePath, featurePath))
                {
                    continue;
                }

                var reference = new ArtifactRef(
                    GeneratorNodeKind.Entity,
                    ArtifactOrigin.Project,
                    entity.Name,
                    FeaturePath: entity.RelativePath,
                    ProjectPath: entity.Path,
                    Namespace: entity.Namespace,
                    DisplayName: string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Name : entity.DisplayName);

                if (seen.Add(CreateEntityKey(reference)))
                {
                    result.Add(CreateItem(reference));
                }
            }
        }

        foreach (var reference in EnumerateSessionArtifacts(GeneratorNodeKind.Entity).Where(reference => MatchesFeaturePath(reference.FeaturePath, featurePath)))
        {
            if (seen.Add(CreateEntityKey(reference)))
            {
                result.Add(CreateItem(reference));
            }
        }

        return result;
    }

    public IReadOnlyList<AvailableArtifactItem> GetRepositories(string? featurePath = null)
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var repository in _projectModel.Repositories)
            {
                var reference = new ArtifactRef(
                    GeneratorNodeKind.Repository,
                    ArtifactOrigin.Project,
                    repository.InterfaceName,
                    FeaturePath: featurePath,
                    ProjectPath: repository.Path,
                    DisplayName: repository.InterfaceName);

                if (seen.Add(CreateRepositoryKey(reference)))
                {
                    result.Add(CreateItem(reference));
                }
            }
        }

        foreach (var reference in EnumerateSessionArtifacts(GeneratorNodeKind.Repository).Where(reference => MatchesFeaturePath(reference.FeaturePath, featurePath)))
        {
            if (seen.Add(CreateRepositoryKey(reference)))
            {
                result.Add(CreateItem(reference));
            }
        }

        return result;
    }

    public IReadOnlyList<AvailableArtifactItem> GetQueries(string? featurePath = null)
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var query in _projectModel.Queries)
            {
                if (!MatchesFeaturePath(query.FeaturePath, featurePath))
                {
                    continue;
                }

                var reference = new ArtifactRef(
                    GeneratorNodeKind.Query,
                    ArtifactOrigin.Project,
                    query.Name,
                    FeaturePath: query.FeaturePath,
                    ProjectPath: query.Path,
                    Namespace: query.Namespace,
                    DisplayName: query.Name);

                if (seen.Add(CreateQueryKey(reference)))
                {
                    result.Add(CreateItem(reference));
                }
            }
        }

        foreach (var reference in EnumerateSessionArtifacts(GeneratorNodeKind.Query).Where(reference => MatchesFeaturePath(reference.FeaturePath, featurePath)))
        {
            if (seen.Add(CreateQueryKey(reference)))
            {
                result.Add(CreateItem(reference));
            }
        }

        return result;
    }

    public AvailableArtifactItem? Find(ArtifactRef artifactRef)
    {
        if (artifactRef.NodeId.HasValue)
        {
            var sessionItem = FindByNodeId(artifactRef.NodeId.Value);
            if (sessionItem is not null)
            {
                return sessionItem;
            }
        }

        if (artifactRef.Origin == ArtifactOrigin.Project)
        {
            return FindProjectArtifact(artifactRef.Kind, artifactRef.Name, artifactRef.FeaturePath);
        }

        return GetItemsByKind(artifactRef.Kind).FirstOrDefault(item =>
            string.Equals(item.Name, artifactRef.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.FeaturePath, artifactRef.FeaturePath, StringComparison.OrdinalIgnoreCase));
    }

    public AvailableArtifactItem? FindByNodeId(Guid nodeId)
    {
        return _session.Traverse()
            .Select(CreateSessionArtifactRef)
            .Where(reference => reference is not null)
            .Select(reference => CreateItem(reference!))
            .FirstOrDefault(item => item.NodeId == nodeId);
    }

    public AvailableArtifactItem? FindProjectArtifact(GeneratorNodeKind kind, string name, string? featurePath = null)
    {
        return GetItemsByKind(kind)
            .FirstOrDefault(item =>
                item.IsFromProject &&
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) &&
                (featurePath is null || string.Equals(item.FeaturePath, featurePath, StringComparison.OrdinalIgnoreCase)));
    }

    public AvailableArtifactItem? FindByName(string name, GeneratorNodeKind kind)
    {
        return GetItemsByKind(kind).FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsFromSession(string name, GeneratorNodeKind kind)
    {
        return FindByName(name, kind)?.IsFromSession == true;
    }

    public bool IsFromProject(string name, GeneratorNodeKind kind)
    {
        return FindByName(name, kind)?.IsFromProject == true;
    }

    public Guid? FindNodeId(string name, GeneratorNodeKind kind)
    {
        return FindByName(name, kind)?.NodeId;
    }

    public void ClearProjectModel()
    {
        _projectModel = null;
    }

    private IReadOnlyList<AvailableArtifactItem> GetItemsByKind(GeneratorNodeKind kind)
    {
        return kind switch
        {
            GeneratorNodeKind.Feature => GetFeatures(),
            GeneratorNodeKind.Dto => GetDtos(),
            GeneratorNodeKind.Entity => GetEntities(),
            GeneratorNodeKind.Repository => GetRepositories(),
            GeneratorNodeKind.Query => GetQueries(),
            _ => [],
        };
    }

    private IEnumerable<ArtifactRef> EnumerateSessionArtifacts(GeneratorNodeKind kind)
    {
        return _session.Traverse()
            .Where(node => node.Kind == kind)
            .Select(CreateSessionArtifactRef)
            .Where(reference => reference is not null)!
            .Cast<ArtifactRef>();
    }

    private static AvailableArtifactItem CreateItem(ArtifactRef reference)
    {
        return new AvailableArtifactItem(
            reference,
            reference.DisplayName ?? reference.Name,
            reference.FeaturePath);
    }

    private static bool MatchesFeaturePath(string? itemFeaturePath, string? requestedFeaturePath)
    {
        return requestedFeaturePath is null ||
               string.Equals(itemFeaturePath, requestedFeaturePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateFeatureKey(ArtifactRef reference)
    {
        return ArtifactKey.From(reference).Value;
    }

    private static string CreateDtoKey(ArtifactRef reference)
    {
        return ArtifactKey.From(reference).Value;
    }

    private static string CreateEntityKey(ArtifactRef reference)
    {
        return ArtifactKey.From(reference).Value;
    }

    private static string CreateRepositoryKey(ArtifactRef reference)
    {
        return ArtifactKey.From(reference).Value;
    }

    private static string CreateQueryKey(ArtifactRef reference)
    {
        return ArtifactKey.From(reference).Value;
    }

    private ArtifactRef? CreateSessionArtifactRef(GeneratorNode node)
    {
        return node.Kind switch
        {
            GeneratorNodeKind.Feature when node.State is FeatureGeneratorState featureState =>
                new ArtifactRef(
                    GeneratorNodeKind.Feature,
                    ArtifactOrigin.Session,
                    featureState.FeatureName,
                    node.Id,
                    FeaturePath: GetFeaturePath(featureState),
                    DisplayName: featureState.FeatureName),

            GeneratorNodeKind.Dto when node.State is DtoGeneratorState dtoState =>
                new ArtifactRef(
                    GeneratorNodeKind.Dto,
                    ArtifactOrigin.Session,
                    GetDtoFullName(dtoState),
                    node.Id,
                    FeaturePath: dtoState.FeatureRef?.FeaturePath,
                    OwnerName: ResolveOwnerQueryName(node),
                    DisplayName: GetDtoFullName(dtoState)),

            GeneratorNodeKind.Entity when node.State is EntityGeneratorState entityState =>
                new ArtifactRef(
                    GeneratorNodeKind.Entity,
                    ArtifactOrigin.Session,
                    entityState.EntityName,
                    node.Id,
                    FeaturePath: ResolveFeaturePath(node, _ => null),
                    Namespace: GetEntityNamespace(entityState),
                    DisplayName: entityState.EntityName),

            GeneratorNodeKind.Repository when node.State is RepositoryGeneratorState repositoryState =>
                new ArtifactRef(
                    GeneratorNodeKind.Repository,
                    ArtifactOrigin.Session,
                    GetRepositoryInterfaceName(repositoryState),
                    node.Id,
                    repositoryState.FeatureRef?.FeaturePath,
                    DisplayName: GetRepositoryInterfaceName(repositoryState)),

            GeneratorNodeKind.Query when node.State is QueryGeneratorState queryState =>
                new ArtifactRef(
                    GeneratorNodeKind.Query,
                    ArtifactOrigin.Session,
                    queryState.QueryName,
                    node.Id,
                    queryState.FeaturePath is { Length: >0 } fp ? fp : null,
                    DisplayName: queryState.QueryName),

            _ => null,
        };
    }

    private string? ResolveOwnerQueryName(GeneratorNode node)
    {
        if (node.ParentId is null || !string.Equals(node.RelationshipName, "ResultDto", StringComparison.Ordinal))
        {
            return null;
        }

        var parent = _session.FindNode(node.ParentId.Value);
        return parent?.State is QueryGeneratorState queryState
            ? StringUtilities.StripSuffix(queryState.QueryName, GeneratorConstants.QuerySuffix)
            : null;
    }

    private string? ResolveFeaturePath(GeneratorNode node, Func<GeneratorNode, string?> currentResolver)
    {
        var current = currentResolver(node);
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        if (node.ParentId is null)
        {
            return null;
        }

        var parent = _session.FindNode(node.ParentId.Value);
        if (parent?.State is QueryGeneratorState queryState)
        {
            return queryState.FeaturePath is { Length: >0 } fp ? fp : null;
        }

        if (parent?.State is RepositoryGeneratorState repositoryState)
        {
            return repositoryState.FeatureRef?.FeaturePath;
        }

        if (parent?.State is CommandGeneratorState commandState)
        {
            return commandState.FeatureRef?.FeaturePath;
        }

        return null;
    }


    private static string GetEntityNamespace(EntityGeneratorState state)
    {
        if (string.IsNullOrWhiteSpace(state.Subfolder))
        {
            return GeneratorConstants.DomainEntitiesNamespace;
        }

        return $"{GeneratorConstants.DomainEntitiesNamespace}.{state.Subfolder.Trim().Replace('/', '.').Replace('\\', '.')}";
    }

    private static string GetRepositoryInterfaceName(RepositoryGeneratorState state)
    {
        if (!string.IsNullOrWhiteSpace(state.InterfaceName) &&
            !string.Equals(state.InterfaceName, "IRepository", StringComparison.OrdinalIgnoreCase))
        {
            return state.InterfaceName;
        }

        var entityName = state.EntityRef?.Name;
        return string.IsNullOrWhiteSpace(entityName)
            ? state.InterfaceName
            : GenerationNaming.GetRepositoryInterfaceName(entityName);
    }

    private static string GetFeaturePath(FeatureGeneratorState state)
    {
        return string.IsNullOrWhiteSpace(state.Subfolder)
            ? state.FeatureName
            : $"{state.Subfolder.Trim().TrimEnd('/', '\\')}/{state.FeatureName}";
    }

    private static string GetDtoFullName(DtoGeneratorState state)
    {
        var suffix = state.SuffixIndex >= 0 && state.SuffixIndex < DtoSuffixes.Count
            ? DtoSuffixes[state.SuffixIndex]
            : string.Empty;
        return state.BaseName + suffix;
    }
}
