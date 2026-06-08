using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class AddCommandScenarioOutlineBuilder : IAddCommandScenarioOutlineBuilder
{
    public IReadOnlyList<ScenarioNodeViewModel> Build(AddCommandFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        return
        [
            new ScenarioNodeViewModel(
                "Feature",
                string.IsNullOrWhiteSpace(formState.FeatureName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.FeatureName ?? "Select a feature"),
            new ScenarioNodeViewModel(
                "Command",
                string.IsNullOrWhiteSpace(formState.CommandName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                string.IsNullOrWhiteSpace(formState.CommandName) ? "Name is not set" : formState.CommandName.Trim()),
            new ScenarioNodeViewModel(
                "Parameters",
                formState.Properties.Count == 0 ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.Properties.Count == 0 ? "No parameters" : $"{formState.Properties.Count} parameter(s)"),
            new ScenarioNodeViewModel(
                "Response",
                ScenarioNodeStatus.Ready,
                string.IsNullOrWhiteSpace(formState.ResponseType) ? "No response type" : formState.ResponseType),
            new ScenarioNodeViewModel(
                "Dependencies",
                formState.Dependencies.Count == 0 ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.Dependencies.Count == 0 ? "No dependencies" : $"{formState.Dependencies.Count} dependency(s)"),
            new ScenarioNodeViewModel(
                "Web Imports",
                ScenarioNodeStatus.Ready,
                formState.UpdateWebImports ? "Update enabled" : "Do not update"),
        ];
    }
}
