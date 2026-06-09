using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class QueryGeneratorState
{
    private ArtifactRef? _featureRef;
    private ArtifactRef? _resultDtoRef;
    private string? _existingResultDtoName;

    public ArtifactRef? FeatureRef
    {
        get => _featureRef;
        set => _featureRef = value;
    }

    public string QueryName { get; set; } = string.Empty;

    public ArtifactRef? ResultDtoRef
    {
        get => _resultDtoRef;
        set => _resultDtoRef = value;
    }

    public ResponseShape ResponseShape { get; set; } = ResponseShape.Single;

    public string FeaturePath
    {
        get => FeatureRef?.FeaturePath ?? string.Empty;
        set => FeatureRef = CreateProjectFeatureRef(value);
    }

    public Guid? ResultDtoNodeId
    {
        get => ResultDtoRef?.NodeId;
        set
        {
            if (!value.HasValue)
            {
                if (ResultDtoRef?.IsFromSession == true)
                {
                    ResultDtoRef = null;
                }

                return;
            }

            ResultDtoRef = new ArtifactRef(
                GeneratorNodeKind.Dto,
                ArtifactOrigin.Session,
                _existingResultDtoName ?? ResultDtoRef?.Name ?? string.Empty,
                value);
        }
    }

    public string? ExistingResultDtoName
    {
        get => ResultDtoRef?.Name ?? _existingResultDtoName;
        set
        {
            _existingResultDtoName = value;

            if (ResultDtoRef?.IsFromSession == true)
            {
                ResultDtoRef = ResultDtoRef with { Name = value ?? string.Empty };
                return;
            }

            ResultDtoRef = string.IsNullOrWhiteSpace(value)
                ? null
                : new ArtifactRef(
                    GeneratorNodeKind.Dto,
                    ArtifactOrigin.Project,
                    value,
                    FeaturePath: FeaturePath);
        }
    }

    public ObservableCollection<PropertySpec> Parameters { get; } = new();

    public bool CreateQueryServiceMethod { get; set; } = true;

    public string MethodName { get; set; } = string.Empty;

    public bool GenerateHandlerBody { get; set; } = true;

    public bool GenerateQueryServiceBody { get; set; } = true;

    public bool UpdateWebImports { get; set; } = true;

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
