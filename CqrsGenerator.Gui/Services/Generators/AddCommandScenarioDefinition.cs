using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddCommandScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<CommandRootSessionViewModel> _factory;

    public AddCommandScenarioDefinition(Func<CommandRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession() => _factory();
}
