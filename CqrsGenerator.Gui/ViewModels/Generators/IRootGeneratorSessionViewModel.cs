using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.ViewModels.Generators;

public interface IRootGeneratorSessionViewModel : IGeneratorSessionViewModel
{
    GenerationActionDescriptor ActionDescriptor { get; }

    bool CanBuildPlan { get; }

    IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes();
}
