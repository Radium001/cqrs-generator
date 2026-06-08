using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed class CreateFeatureScenarioOutlineBuilder : ICreateFeatureScenarioOutlineBuilder
{
    public IReadOnlyList<ScenarioNodeViewModel> Build(CreateFeatureFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        return
        [
            new ScenarioNodeViewModel(
                "Feature",
                string.IsNullOrWhiteSpace(formState.FeatureName)
                    ? ScenarioNodeStatus.Missing
                    : ScenarioNodeStatus.Ready,
                formState.FeatureName ?? "Enter a feature name"),

            new ScenarioNodeViewModel(
                "Web Feature",
                formState.CreateWebFeature ? ScenarioNodeStatus.Ready : ScenarioNodeStatus.Draft,
                formState.CreateWebFeature
                    ? "Web directory will be created"
                    : "Web directory will not be created"),
        ];
    }
}
