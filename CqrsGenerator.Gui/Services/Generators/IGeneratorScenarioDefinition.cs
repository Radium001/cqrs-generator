using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public interface IGeneratorScenarioDefinition
{
    GenerationActionDescriptor Descriptor { get; }

    bool IsAvailable(WorkspaceState state);

    IRootGeneratorSessionViewModel CreateRootSession();
}
