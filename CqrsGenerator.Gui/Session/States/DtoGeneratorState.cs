using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.Session.States;

public sealed class DtoGeneratorState
{
    public string BaseName { get; set; } = string.Empty;

    public ArtifactRef? FeatureRef { get; set; }

    public string? FeaturePath
    {
        get => FeatureRef?.FeaturePath;
        set => FeatureRef = string.IsNullOrWhiteSpace(value)
            ? null
            : new ArtifactRef(
                GeneratorNodeKind.Feature,
                ArtifactOrigin.Project,
                value,
                FeaturePath: value,
                DisplayName: value);
    }

    public int SuffixIndex { get; set; }

    public string? Subfolder { get; set; }

    public bool UpdateWebImports { get; set; } = true;

    public ObservableCollection<PropertySpec> Properties { get; } = new();
}
