using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddQueryScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<AddQueryRootSessionViewModel> _factory;

    public AddQueryScenarioDefinition(Func<AddQueryRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession() => _factory();
}
