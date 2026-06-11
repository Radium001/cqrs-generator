using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class AddQueryScenarioOutlineBuilder : IAddQueryScenarioOutlineBuilder
{
    public IReadOnlyList<ScenarioNodeViewModel> Build(AddQueryFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        var resultText = formState.DtoSelection?.DtoName ?? "Choose DTO";

        return
        [
            new ScenarioNodeViewModel(
                "Feature",
                string.IsNullOrWhiteSpace(formState.FeatureName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.FeatureName ?? "Select a feature"),
            new ScenarioNodeViewModel(
                "Query",
                string.IsNullOrWhiteSpace(formState.QueryName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                string.IsNullOrWhiteSpace(formState.QueryName) ? "Name is not set" : formState.QueryName.Trim()),
            new ScenarioNodeViewModel(
                "Parameters",
                formState.Parameters.Count == 0 ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.Parameters.Count == 0 ? "No parameters" : $"{formState.Parameters.Count} parameter(s)"),
            new ScenarioNodeViewModel(
                "Result",
                string.IsNullOrWhiteSpace(formState.DtoSelection?.DtoName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                resultText),
            new ScenarioNodeViewModel(
                "Query Service",
                formState.QueryService is null ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                formState.QueryService?.InterfaceName ?? "No query service"),
            new ScenarioNodeViewModel(
                "Method",
                !formState.CreateQueryServiceMethod
                    ? ScenarioNodeStatus.Ready
                    : string.IsNullOrWhiteSpace(formState.MethodName) ? ScenarioNodeStatus.Missing : ScenarioNodeStatus.Ready,
                !formState.CreateQueryServiceMethod
                    ? "Method disabled"
                    : string.IsNullOrWhiteSpace(formState.MethodName) ? "Name is not set" : formState.MethodName.Trim()),
            new ScenarioNodeViewModel(
                "Web Imports",
                ScenarioNodeStatus.Ready,
                formState.UpdateWebImports ? "Update enabled" : "Do not update"),
            new ScenarioNodeViewModel(
                "Custom DTO Properties",
                formState.CustomDtoProperties is null || formState.CustomDtoProperties.Count == 0
                    ? ScenarioNodeStatus.Missing
                    : ScenarioNodeStatus.Ready,
                formState.CustomDtoProperties is null || formState.CustomDtoProperties.Count == 0
                    ? "No custom properties"
                    : $"{formState.CustomDtoProperties.Count} custom property/properties"),
        ];
    }
}
