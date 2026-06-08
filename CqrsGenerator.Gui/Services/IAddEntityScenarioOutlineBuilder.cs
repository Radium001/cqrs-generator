using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IAddEntityScenarioOutlineBuilder
{
    IReadOnlyList<ScenarioNodeViewModel> Build(AddEntityFormState formState);
}
