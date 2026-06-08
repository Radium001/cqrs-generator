using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services.Generators;

public interface IGeneratorCatalog
{
    IReadOnlyList<IGeneratorScenarioDefinition> GetDefinitions();

    IGeneratorScenarioDefinition GetDefinition(string actionId);

    IReadOnlyList<GenerationActionDescriptor> GetDescriptors();
}
