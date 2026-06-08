using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Services.Generators;
using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddDtoScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<IEmbeddedSessionHost, DtoRootSessionViewModel> _factory;

    public AddDtoScenarioDefinition(Func<IEmbeddedSessionHost, DtoRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-dto", "Add DTO", "Application", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession(IEmbeddedSessionHost embeddedSessionHost) =>
        _factory(embeddedSessionHost);
}
