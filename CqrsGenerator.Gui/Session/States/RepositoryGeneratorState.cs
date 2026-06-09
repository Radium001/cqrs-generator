namespace CqrsGenerator.Gui.Session.States;

public sealed class RepositoryGeneratorState
{
    private ArtifactRef? _featureRef;
    private ArtifactRef? _entityRef;

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

    public string InterfaceName { get; set; } = string.Empty;

    public string ImplementationName { get; set; } = string.Empty;

    public ArtifactRef? EntityRef
    {
        get => _entityRef;
        set => _entityRef = value;
    }

    public Guid? EntityNodeId
    {
        get => EntityRef?.NodeId;
        set
        {
            if (!value.HasValue)
            {
                if (EntityRef?.IsFromSession == true)
                {
                    EntityRef = null;
                }

                return;
            }

            EntityRef = new ArtifactRef(
                GeneratorNodeKind.Entity,
                ArtifactOrigin.Session,
                EntityRef?.Name ?? string.Empty,
                value);
        }
    }

    public bool AddDependencyInjectionRegistration { get; set; } = true;

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
