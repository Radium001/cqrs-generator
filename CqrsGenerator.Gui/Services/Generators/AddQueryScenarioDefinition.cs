using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddQueryScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<IEmbeddedSessionHost, AddQueryRootSessionViewModel> _factory;

    public AddQueryScenarioDefinition(Func<IEmbeddedSessionHost, AddQueryRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession(IEmbeddedSessionHost embeddedSessionHost) =>
        _factory(embeddedSessionHost);
}
