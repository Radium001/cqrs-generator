using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IAddWebPageScenarioOutlineBuilder
{
    IReadOnlyList<ScenarioNodeViewModel> Build(AddWebPageFormState formState);
}
