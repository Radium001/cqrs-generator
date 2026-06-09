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

    public IReadOnlyList<AvailableArtifactItem> GetDtos(string? featurePath = null)
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var dto in _projectModel.Dtos)
            {
                if (featurePath is null ||
                    string.Equals(dto.FeaturePath, featurePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add(dto.Name))
                    {
                        result.Add(new AvailableArtifactItem(
                            dto.Name,
                            GeneratorNodeKind.Dto,
                            null,
                            false,
                            true,
                            dto.FeaturePath));
                    }
                }
            }
        }

        foreach (var node in _session.Traverse())
        {
            if (node.Kind == GeneratorNodeKind.Dto && node.State is DtoGeneratorState dtoState)
            {
                var name = GetDtoFullName(dtoState);
                if (seen.Add(name))
                {
                    result.Add(new AvailableArtifactItem(
                        name,
                        GeneratorNodeKind.Dto,
                        node.Id,
                        true,
                        false));
                }
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
                if (featurePath is null ||
                    string.Equals(entity.RelativePath, featurePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add(entity.Name))
                    {
                        result.Add(new AvailableArtifactItem(
                            entity.Name,
                            GeneratorNodeKind.Entity,
                            null,
                            false,
                            true,
                            entity.RelativePath));
                    }
                }
            }
        }

        foreach (var node in _session.Traverse())
        {
            if (node.Kind == GeneratorNodeKind.Entity && node.State is EntityGeneratorState entityState)
            {
                if (seen.Add(entityState.EntityName))
                {
                    result.Add(new AvailableArtifactItem(
                        entityState.EntityName,
                        GeneratorNodeKind.Entity,
                        node.Id,
                        true,
                        false));
                }
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
                if (featurePath is null ||
                    string.Equals(query.FeaturePath, featurePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add(query.Name))
                    {
                        result.Add(new AvailableArtifactItem(
                            query.Name,
                            GeneratorNodeKind.Query,
                            null,
                            false,
                            true,
                            query.FeaturePath));
                    }
                }
            }
        }

        foreach (var node in _session.Traverse())
        {
            if (node.Kind == GeneratorNodeKind.Query && node.State is QueryGeneratorState queryState)
            {
                if (seen.Add(queryState.QueryName))
                {
                    result.Add(new AvailableArtifactItem(
                        queryState.QueryName,
                        GeneratorNodeKind.Query,
                        node.Id,
                        true,
                        false,
                        queryState.FeaturePath));
                }
            }
        }

        return result;
    }

    public IReadOnlyList<AvailableArtifactItem> GetRepositories()
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var repo in _projectModel.Repositories)
            {
                if (seen.Add(repo.InterfaceName))
                {
                    result.Add(new AvailableArtifactItem(
                        repo.InterfaceName,
                        GeneratorNodeKind.Repository,
                        null,
                        false,
                        true));
                }
            }
        }

        foreach (var node in _session.Traverse())
        {
            if (node.Kind == GeneratorNodeKind.Repository && node.State is RepositoryGeneratorState repoState)
            {
                if (seen.Add(repoState.InterfaceName))
                {
                    result.Add(new AvailableArtifactItem(
                        repoState.InterfaceName,
                        GeneratorNodeKind.Repository,
                        node.Id,
                        true,
                        false));
                }
            }
        }

        return result;
    }

    public IReadOnlyList<AvailableArtifactItem> GetFeatures()
    {
        var result = new List<AvailableArtifactItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_projectModel is not null)
        {
            foreach (var feature in _projectModel.Features)
            {
                if (seen.Add(feature.Name))
                {
                    result.Add(new AvailableArtifactItem(
                        feature.Name,
                        GeneratorNodeKind.Feature,
                        null,
                        false,
                        true,
                        feature.RelativePath));
                }
            }
        }

        foreach (var node in _session.Traverse())
        {
            if (node.Kind == GeneratorNodeKind.Feature && node.State is FeatureGeneratorState featureState)
            {
                if (seen.Add(featureState.FeatureName))
                {
                    result.Add(new AvailableArtifactItem(
                        featureState.FeatureName,
                        GeneratorNodeKind.Feature,
                        node.Id,
                        true,
                        false));
                }
            }
        }

        return result;
    }

    public AvailableArtifactItem? FindByName(string name, GeneratorNodeKind kind)
    {
        var items = kind switch
        {
            GeneratorNodeKind.Dto => GetDtos(),
            GeneratorNodeKind.Entity => GetEntities(),
            GeneratorNodeKind.Query => GetQueries(),
            GeneratorNodeKind.Repository => GetRepositories(),
            GeneratorNodeKind.Feature => GetFeatures(),
            _ => [],
        };

        return items.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
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

    private static string GetDtoFullName(DtoGeneratorState state)
    {
        var suffix = state.SuffixIndex >= 0 && state.SuffixIndex < DtoSuffixes.Count
            ? DtoSuffixes[state.SuffixIndex]
            : string.Empty;
        return state.BaseName + suffix;
    }
}
