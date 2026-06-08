using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IAddDtoScenarioOutlineBuilder
{
    IReadOnlyList<ScenarioNodeViewModel> Build(AddDtoFormState formState);
}
