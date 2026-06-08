using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddEntityScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<IEmbeddedSessionHost, EntityRootSessionViewModel> _factory;

    public AddEntityScenarioDefinition(Func<IEmbeddedSessionHost, EntityRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-entity", "Add Entity", "Domain", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession(IEmbeddedSessionHost embeddedSessionHost) => _factory(embeddedSessionHost);
}
