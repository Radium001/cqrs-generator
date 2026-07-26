using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class WebFeatureChoiceViewModel
{
    private WebFeatureChoiceViewModel(string name, ArtifactRef? reference)
    {
        Name = name;
        Ref = reference;
    }

    public string Name { get; }

    public ArtifactRef? Ref { get; }

    public string RelativePath => Ref?.FeaturePath ?? string.Empty;

    public bool UpdatesImports => Ref is not null;

    public static WebFeatureChoiceViewModel DoNotUpdate { get; } =
        new("Do not update web imports", null);

    public static WebFeatureChoiceViewModel FromProject(string name, string relativePath, string path) =>
        new(
            name,
            new ArtifactRef(
                GeneratorNodeKind.Feature,
                ArtifactOrigin.Project,
                name,
                FeaturePath: relativePath,
                ProjectPath: path,
                DisplayName: relativePath));

    public override string ToString() => Name;
}
