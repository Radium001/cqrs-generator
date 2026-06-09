using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public static class ScenarioOutlineProjector
{
    public static IReadOnlyList<ScenarioNodeViewModel> Project(GeneratorNode node)
    {
        return [new ScenarioNodeViewModel(node)];
    }
}
