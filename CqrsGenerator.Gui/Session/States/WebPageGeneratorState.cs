using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CqrsGenerator.Gui.Session.States;

public sealed class WebPageGeneratorState
{
    private bool _syncingQueries;
    private ArtifactRef? _featureRef;

    public WebPageGeneratorState()
    {
        QueryRefs.CollectionChanged += OnQueryRefsChanged;
        QueryNodeIds.CollectionChanged += OnQueryNodeIdsChanged;
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

    public string PageName { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public ObservableCollection<ArtifactRef> QueryRefs { get; } = new();

    public ObservableCollection<Guid> QueryNodeIds { get; } = new();

    private void OnQueryRefsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingQueries)
        {
            return;
        }

        _syncingQueries = true;
        try
        {
            QueryNodeIds.Clear();
            foreach (var reference in QueryRefs.Where(reference => reference.NodeId.HasValue))
            {
                QueryNodeIds.Add(reference.NodeId!.Value);
            }
        }
        finally
        {
            _syncingQueries = false;
        }
    }

    private void OnQueryNodeIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingQueries)
        {
            return;
        }

        _syncingQueries = true;
        try
        {
            QueryRefs.Clear();
            foreach (var nodeId in QueryNodeIds)
            {
                QueryRefs.Add(new ArtifactRef(
                    GeneratorNodeKind.Query,
                    ArtifactOrigin.Session,
                    string.Empty,
                    nodeId));
            }
        }
        finally
        {
            _syncingQueries = false;
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
