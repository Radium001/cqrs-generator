namespace CqrsGenerator.Gui.Session;

public readonly record struct ArtifactKey(string Value)
{
    public static ArtifactKey From(ArtifactRef reference)
    {
        if (reference.NodeId is Guid nodeId)
        {
            return new ArtifactKey($"session:{reference.Kind}:{nodeId:D}");
        }

        var featurePath = Normalize(reference.FeaturePath);
        var projectPath = Normalize(reference.ProjectPath);
        var name = Normalize(reference.Name);
        var owner = Normalize(reference.OwnerName);
        var ns = Normalize(reference.Namespace);
        return new ArtifactKey($"project:{reference.Kind}:{reference.Origin}:{featurePath}:{projectPath}:{owner}:{ns}:{name}");
    }

    public static string For(object? item)
    {
        return item switch
        {
            AvailableArtifactItem artifact => From(artifact.Ref).Value,
            ArtifactRef reference => From(reference).Value,
            _ => item?.ToString() ?? string.Empty,
        };
    }

    public static bool Equals(ArtifactRef? left, ArtifactRef? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(From(left).Value, From(right).Value, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
