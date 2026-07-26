using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class QueryGeneratorState
{
    public string QueryName { get; set; } = string.Empty;

    public ResponseShape ResponseShape { get; set; } = ResponseShape.Single;

    public ArtifactRef? FeatureRef { get; set; }

    public ArtifactRef? ResultDtoRef { get; set; }

    public string FeaturePath
    {
        get => FeatureRef?.FeaturePath ?? string.Empty;
        set => FeatureRef = CreateProjectFeatureRef(value);
    }

    public string? CustomDtoName { get; set; }

    public ObservableCollection<PropertySpec> Parameters { get; } = new();

    public bool CreateQueryServiceMethod { get; set; } = true;

    public string MethodName { get; set; } = string.Empty;

    public bool GenerateHandlerBody { get; set; } = true;

    public bool GenerateQueryServiceBody { get; set; } = true;

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
