namespace CqrsGenerator.Gui.Session;

public sealed class ReferenceRegistry
{
    private readonly Dictionary<(Guid SourceId, string Relationship), ArtifactRef> _refs = new();

    public void SetRef(Guid sourceNodeId, string relationship, ArtifactRef? target)
    {
        if (target is null)
        {
            ClearRef(sourceNodeId, relationship);
            return;
        }

        _refs[(sourceNodeId, relationship)] = target;
    }

    public void ClearRef(Guid sourceNodeId, string relationship)
    {
        _refs.Remove((sourceNodeId, relationship));
    }

    public ArtifactRef? GetRef(Guid sourceNodeId, string relationship)
    {
        return _refs.TryGetValue((sourceNodeId, relationship), out var ref_) ? ref_ : null;
    }

    public IReadOnlyList<(Guid SourceId, string Relationship)> FindTargeting(Guid targetNodeId)
    {
        return _refs
            .Where(kvp => kvp.Value.NodeId == targetNodeId)
            .Select(kvp => (kvp.Key.SourceId, kvp.Key.Relationship))
            .ToList();
    }

    public void ClearTargeting(Guid targetNodeId)
    {
        var toRemove = _refs
            .Where(kvp => kvp.Value.NodeId == targetNodeId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _refs.Remove(key);
        }
    }

    public void ClearAllForSource(Guid sourceNodeId)
    {
        var toRemove = _refs
            .Where(kvp => kvp.Key.SourceId == sourceNodeId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _refs.Remove(key);
        }
    }
}
