using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IAddCommandScenarioOutlineBuilder
{
    IReadOnlyList<ScenarioNodeViewModel> Build(AddCommandFormState formState);
}
