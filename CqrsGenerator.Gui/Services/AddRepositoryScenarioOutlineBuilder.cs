using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class AddRepositoryScenarioOutlineBuilder : IAddRepositoryScenarioOutlineBuilder
{
    public IReadOnlyList<ScenarioNodeViewModel> Build(AddRepositoryFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        var entityTitle = formState.CustomEntity is not null
            ? $"{formState.CustomEntity.EntityName.Trim()} (new)"
            : formState.ExistingEntityDisplayName ?? "Select an entity";

        var methodsCount = formState.SelectedMethodPresetKeys.Count + formState.CustomMethods.Count;

        return
        [
            new ScenarioNodeViewModel(
                "Entity",
                string.IsNullOrWhiteSpace(entityTitle) || entityTitle == "Select an entity" ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                entityTitle),
            new ScenarioNodeViewModel(
                "Methods",
                ScenarioNodeStatus.Ready,
                methodsCount == 0 ? "Default methods" : $"{methodsCount} method(s)"),
            new ScenarioNodeViewModel(
                "DI",
                ScenarioNodeStatus.Ready,
                formState.AddDependencyInjectionRegistration ? "Registration enabled" : "Do not register"),
        ];
    }
}
