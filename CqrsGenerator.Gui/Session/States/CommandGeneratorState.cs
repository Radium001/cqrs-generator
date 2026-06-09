using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class CommandGeneratorState
{
    private bool _syncingRepositories;
    private ArtifactRef? _featureRef;

    public CommandGeneratorState()
    {
        RepositoryRefs.CollectionChanged += OnRepositoryRefsChanged;
        RepositoryNodeIds.CollectionChanged += OnRepositoryNodeIdsChanged;
    }

    public ArtifactRef? FeatureRef
    {
        get => _featureRef;
        set => _featureRef = value;
    }

    public string FeaturePath
    {
        get => FeatureRef?.FeaturePath ?? string.Empty;
        set => FeatureRef = CreateProjectFeatureRef(value);
    }

    public string CommandName { get; set; } = string.Empty;

    public string? ResponseType { get; set; }

    public ObservableCollection<PropertySpec> Parameters { get; } = new();

    public ObservableCollection<ArtifactRef> RepositoryRefs { get; } = new();

    public ObservableCollection<Guid> RepositoryNodeIds { get; } = new();

    private void OnRepositoryRefsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingRepositories)
        {
            return;
        }

        _syncingRepositories = true;
        try
        {
            RepositoryNodeIds.Clear();
            foreach (var reference in RepositoryRefs.Where(reference => reference.NodeId.HasValue))
            {
                RepositoryNodeIds.Add(reference.NodeId!.Value);
            }
        }
        finally
        {
            _syncingRepositories = false;
        }
    }

    private void OnRepositoryNodeIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingRepositories)
        {
            return;
        }

        _syncingRepositories = true;
        try
        {
            RepositoryRefs.Clear();
            foreach (var nodeId in RepositoryNodeIds)
            {
                RepositoryRefs.Add(new ArtifactRef(
                    GeneratorNodeKind.Repository,
                    ArtifactOrigin.Session,
                    string.Empty,
                    nodeId));
            }
        }
        finally
        {
            _syncingRepositories = false;
        }
    }

    private static ArtifactRef? CreateProjectFeatureRef(string? featurePath)
    {
        if (string.IsNullOrWhiteSpace(featurePath))
        {
            return null;
        }

        var trimmedPath = featurePath.Trim();
        var name = trimmedPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? trimmedPath;
        return new ArtifactRef(
            GeneratorNodeKind.Feature,
            ArtifactOrigin.Project,
            name,
            FeaturePath: trimmedPath,
            DisplayName: name);
    }
}
