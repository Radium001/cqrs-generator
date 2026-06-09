using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Services;

public interface IWorkspaceSessionService
{
    Task OpenProjectAsync(CancellationToken cancellationToken);

    Task RescanProjectAsync(CancellationToken cancellationToken);

    void SelectAction(GenerationActionDescriptor? action, IRootGeneratorSessionViewModel? rootSession);

    void RefreshRootSessionState(IRootGeneratorSessionViewModel? rootSession);

    Task BuildPlanAsync(IRootGeneratorSessionViewModel? rootSession, CancellationToken cancellationToken);

    Task ApplyPlanAsync(CancellationToken cancellationToken);
}
