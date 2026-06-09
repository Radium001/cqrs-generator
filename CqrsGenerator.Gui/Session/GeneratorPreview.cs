namespace CqrsGenerator.Gui.Session;

public sealed class GeneratorPreview
{
    public GeneratorNode Node { get; }

    public string Summary { get; }

    public IReadOnlyList<string> FilesToCreate { get; }

    public IReadOnlyList<string> FilesToUpdate { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<string> Conflicts { get; }

    public GeneratorPreview(
        GeneratorNode node,
        string summary,
        IReadOnlyList<string> filesToCreate,
        IReadOnlyList<string> filesToUpdate,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> conflicts)
    {
        Node = node;
        Summary = summary;
        FilesToCreate = filesToCreate;
        FilesToUpdate = filesToUpdate;
        Warnings = warnings;
        Conflicts = conflicts;
    }
}
