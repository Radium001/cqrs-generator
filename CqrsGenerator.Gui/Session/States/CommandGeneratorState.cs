using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class CommandGeneratorState
{
    private ArtifactRef? _featureRef;

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

    public bool GenerateHandlerBody { get; set; } = true;

    public ObservableCollection<PropertySpec> Parameters { get; } = new();

    public ObservableCollection<ArtifactRef> RepositoryRefs { get; } = new();

    public ObservableCollection<string> StandardDependencyNames { get; } = new();

    public ArtifactRef? WebFeatureRef { get; set; }

    public bool HasWebFeatureSelection { get; set; }

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
