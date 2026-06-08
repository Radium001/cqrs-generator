using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class GeneratorCatalog : IGeneratorCatalog
{
    private readonly IReadOnlyList<IGeneratorScenarioDefinition> _definitions;

    public GeneratorCatalog(IEnumerable<IGeneratorScenarioDefinition> definitions)
    {
        _definitions = definitions.OrderBy(definition => definition.Descriptor.GroupName, StringComparer.Ordinal)
            .ThenBy(definition => definition.Descriptor.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<IGeneratorScenarioDefinition> GetDefinitions() => _definitions;

    public IGeneratorScenarioDefinition GetDefinition(string actionId) =>
        _definitions.First(definition => string.Equals(definition.Descriptor.ActionId, actionId, StringComparison.Ordinal));

    public IReadOnlyList<GenerationActionDescriptor> GetDescriptors() =>
        _definitions.Select(definition => definition.Descriptor).ToArray();
}
