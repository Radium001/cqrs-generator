using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface IAddRepositoryScenarioOutlineBuilder
{
    IReadOnlyList<ScenarioNodeViewModel> Build(AddRepositoryFormState formState);
}
