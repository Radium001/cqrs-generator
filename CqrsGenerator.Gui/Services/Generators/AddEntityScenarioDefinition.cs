using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddEntityScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<EntityRootSessionViewModel> _factory;

    public AddEntityScenarioDefinition(Func<EntityRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-entity", "Add Entity", "Domain", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession() => _factory();
}
