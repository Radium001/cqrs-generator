using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IAddQueryScenarioOutlineBuilder
{
    IReadOnlyList<ScenarioNodeViewModel> Build(AddQueryFormState formState);
}
