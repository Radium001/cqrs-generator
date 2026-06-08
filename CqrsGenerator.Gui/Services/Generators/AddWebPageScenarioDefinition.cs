using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddWebPageScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<IEmbeddedSessionHost, AddWebPageRootSessionViewModel> _factory;

    public AddWebPageScenarioDefinition(Func<IEmbeddedSessionHost, AddWebPageRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-web-page", "Add Web Page", "UI", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession(IEmbeddedSessionHost embeddedSessionHost) =>
        _factory(embeddedSessionHost);
}
