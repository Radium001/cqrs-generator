using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class AddEntityScenarioOutlineBuilder : IAddEntityScenarioOutlineBuilder
{
    public IReadOnlyList<ScenarioNodeViewModel> Build(AddEntityFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        var sourceText = formState.SourceMode == EntitySourceMode.EfEntity
            ? formState.SelectedEfEntity?.Name ?? "Select EF entity"
            : "Create from scratch";

        return
        [
            new ScenarioNodeViewModel(
                "Source",
                formState.SourceMode == EntitySourceMode.EfEntity && formState.SelectedEfEntity is null
                    ? ScenarioNodeStatus.Missing
                    : ScenarioNodeStatus.Ready,
                sourceText),
            new ScenarioNodeViewModel(
                "Entity",
                string.IsNullOrWhiteSpace(formState.EntityName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                string.IsNullOrWhiteSpace(formState.EntityName) ? "Name is not set" : formState.EntityName.Trim()),
            new ScenarioNodeViewModel(
                "Properties",
                formState.ManualProperties.Count == 0 && formState.SelectedEfPropertyNames.Count == 0
                    ? ScenarioNodeStatus.Missing
                    : ScenarioNodeStatus.Ready,
                $"{formState.SelectedEfPropertyNames.Count} EF field(s), {formState.ManualProperties.Count} manual propertie(s)"),
            new ScenarioNodeViewModel(
                "Options",
                ScenarioNodeStatus.Ready,
                $"Factory: {(formState.GenerateFactoryMethod ? "On" : "Off")}, EF mapping: {(formState.GenerateEfMapping ? "On" : "Off")}"),
        ];
    }
}
