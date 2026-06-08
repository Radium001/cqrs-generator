using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Services.Generators;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class ActionLauncherViewModel
{
    public ActionLauncherViewModel(IGeneratorCatalog generatorCatalog, IWorkspaceStore workspaceStore, Action<GenerationActionDescriptor> openAction)
    {
        var items = generatorCatalog.GetDefinitions()
            .Select(definition => new
            {
                Definition = definition,
                Item = new ActionLauncherItemViewModel(
                    definition.Descriptor,
                    new RelayCommand(
                        () => openAction(definition.Descriptor),
                        () => definition.Descriptor.IsImplemented && definition.IsAvailable(workspaceStore.State)))
            })
            .ToArray();

        Groups = items
            .GroupBy(entry => entry.Definition.Descriptor.GroupName, entry => entry.Item)
            .Select(group => new ActionGroupViewModel(group.Key, group))
            .ToArray();

        workspaceStore.StateChanged += (_, _) =>
        {
            foreach (var item in items)
            {
                item.Item.OpenCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public IReadOnlyList<ActionGroupViewModel> Groups { get; }
}
