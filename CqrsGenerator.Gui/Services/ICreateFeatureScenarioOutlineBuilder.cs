using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public interface ICreateFeatureScenarioOutlineBuilder
{
    IReadOnlyList<ScenarioNodeViewModel> Build(CreateFeatureFormState formState);
}
