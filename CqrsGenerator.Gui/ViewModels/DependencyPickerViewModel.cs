using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class DependencyPickerViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> StandardDeps = [.. GeneratorConstants.StandardDependencies];

    public DependencyPickerViewModel()
    {
        Picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item is CommandDependencyOption option ? option.InterfaceName : item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item is CommandDependencyOption option ? option.InterfaceName : item?.ToString() ?? string.Empty,
        };
    }

    public MultiSelectListPickerViewModel Picker { get; }

    [ObservableProperty]
    private IRelayCommand? _createRepositoryCommand;

    [ObservableProperty]
    private bool _hasCreateRepository;

    public void SetDiscovered(IReadOnlyList<RepositoryInfo> repos)
    {
        var items = new List<object>();

        foreach (var dep in StandardDeps)
            items.Add(new CommandDependencyOption(dep));

        foreach (var repo in repos.OrderBy(r => r.InterfaceName, StringComparer.OrdinalIgnoreCase))
            items.Add(new CommandDependencyOption(repo.InterfaceName));

        Picker.SetDiscovered(items);
    }

    public void AddGeneratedDependency(string interfaceName, bool isSelected = true, Action<object>? onEdit = null, Action<object>? onRemove = null)
    {
        Picker.AddRuntime(new CommandDependencyOption(interfaceName, true), isSelected, canEdit: true, canRemove: true, onEdit, onRemove);
    }

    public IReadOnlyList<CommandHandlerDependency> GetSelected()
    {
        return Picker.SelectedDisplayTexts
            .Select(name => new CommandHandlerDependency(name, GenerationNaming.ToDependencyName(name)))
            .ToList();
    }
}
