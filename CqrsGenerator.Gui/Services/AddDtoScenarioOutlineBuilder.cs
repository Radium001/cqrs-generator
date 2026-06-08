using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class AddDtoScenarioOutlineBuilder : IAddDtoScenarioOutlineBuilder
{
    public IReadOnlyList<ScenarioNodeViewModel> Build(AddDtoFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        return
        [
            new ScenarioNodeViewModel(
                "Feature",
                string.IsNullOrWhiteSpace(formState.FeatureName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.FeatureName ?? "Select a feature"),
            new ScenarioNodeViewModel(
                "DTO",
                string.IsNullOrWhiteSpace(formState.DtoName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                string.IsNullOrWhiteSpace(formState.DtoName) ? "Name is not set" : formState.DtoName.Trim()),
            new ScenarioNodeViewModel(
                "Properties",
                formState.Properties.Count == 0 ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.Properties.Count == 0 ? "No properties" : $"{formState.Properties.Count} propertie(s)"),
            new ScenarioNodeViewModel(
                "Subfolder",
                ScenarioNodeStatus.Ready,
                string.IsNullOrWhiteSpace(formState.Subfolder) ? "Root DTOs folder" : formState.Subfolder),
            new ScenarioNodeViewModel(
                "Web Imports",
                ScenarioNodeStatus.Ready,
                formState.UpdateWebImports ? "Update enabled" : "Do not update"),
        ];
    }
}
