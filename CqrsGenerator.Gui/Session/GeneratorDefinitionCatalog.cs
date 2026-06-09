namespace CqrsGenerator.Gui.Session;

public sealed class GeneratorDefinitionCatalog
{
    private readonly Dictionary<GeneratorNodeKind, IGeneratorDefinition> _definitions;

    public GeneratorDefinitionCatalog(IEnumerable<IGeneratorDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Kind);
    }

    public IGeneratorDefinition GetDefinition(GeneratorNodeKind kind)
    {
        return _definitions.GetValueOrDefault(kind)
            ?? throw new InvalidOperationException($"No definition registered for kind '{kind}'.");
    }

    public bool HasDefinition(GeneratorNodeKind kind)
    {
        return _definitions.ContainsKey(kind);
    }

    public IReadOnlyList<IGeneratorDefinition> GetAllDefinitions()
    {
        return _definitions.Values.ToList();
    }
}
