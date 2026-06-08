using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class AddWebPageScenarioOutlineBuilder : IAddWebPageScenarioOutlineBuilder
{
    public IReadOnlyList<ScenarioNodeViewModel> Build(AddWebPageFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        return
        [
            new ScenarioNodeViewModel(
                "Web Feature",
                string.IsNullOrWhiteSpace(formState.WebFeatureName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.WebFeatureName ?? "Select a web feature"),
            new ScenarioNodeViewModel(
                "Page Name",
                string.IsNullOrWhiteSpace(formState.PageName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                string.IsNullOrWhiteSpace(formState.PageName) ? "Name is not set" : formState.PageName.Trim()),
            new ScenarioNodeViewModel(
                "Route",
                string.IsNullOrWhiteSpace(formState.Route) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.Route ?? "Auto-generate from feature"),
            new ScenarioNodeViewModel(
                "Queries",
                formState.QueryBindings.Count == 0 ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.QueryBindings.Count == 0 ? "No queries" : $"{formState.QueryBindings.Count} querie(s)"),
            new ScenarioNodeViewModel(
                "Web Imports",
                ScenarioNodeStatus.Ready,
                formState.CreateImports ? "Update enabled" : "Do not update"),
        ];
    }
}
