using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services.Generators;

public sealed class AddRepositoryScenarioDefinition : IGeneratorScenarioDefinition
{
    private readonly Func<RepositoryRootSessionViewModel> _factory;

    public AddRepositoryScenarioDefinition(Func<RepositoryRootSessionViewModel> factory)
    {
        _factory = factory;
        Descriptor = new GenerationActionDescriptor("add-repository", "Add Repository", "Application", "Ready", true);
    }

    public GenerationActionDescriptor Descriptor { get; }

    public bool IsAvailable(WorkspaceState state) => true;

    public IRootGeneratorSessionViewModel CreateRootSession() => _factory();
}
