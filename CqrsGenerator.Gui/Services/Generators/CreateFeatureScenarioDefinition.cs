using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class CreateFeatureScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<CreateFeatureRootSessionViewModel> _factory;

    public CreateFeatureScenarioDefinition(Func<CreateFeatureRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("new-feature", "New Feature", "Application", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession(IEmbeddedSessionHost embeddedSessionHost) =>
        _factory();
}
