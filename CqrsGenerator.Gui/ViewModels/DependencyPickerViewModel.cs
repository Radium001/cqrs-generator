using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class DependencyPickerViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> StandardDeps = [.. GeneratorConstants.StandardDependencies];
    private readonly Dictionary<string, CommandDependencyOption> _optionsByKey = new(StringComparer.Ordinal);

    public DependencyPickerViewModel()
    {
        Picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item is CommandDependencyOption option ? option.InterfaceName : item?.ToString() ?? string.Empty,
            ItemKeySelector = GetOptionKey,
            ItemCanEditSelector = item => item is CommandDependencyOption option && option.NodeId.HasValue,
            ItemCanRemoveSelector = item => item is CommandDependencyOption option && option.NodeId.HasValue,
        };
        Picker.EditItemRequested = item =>
        {
            if (item is CommandDependencyOption option)
            {
                EditRequested?.Invoke(option);
            }
        };
        Picker.RemoveItemRequested = item =>
        {
            if (item is CommandDependencyOption option)
            {
                RemoveRequested?.Invoke(option);
            }
        };
    }

    public MultiSelectListPickerViewModel Picker { get; }

    public Action<CommandDependencyOption>? EditRequested { get; set; }

    public Action<CommandDependencyOption>? RemoveRequested { get; set; }

    [ObservableProperty]
    private IRelayCommand? _createRepositoryCommand;

    [ObservableProperty]
    private bool _hasCreateRepository;

    public void SetDiscovered(IReadOnlyList<RepositoryInfo> repos)
    {
        var items = new List<object>();
        _optionsByKey.Clear();

        foreach (var dep in StandardDeps)
        {
            var option = new CommandDependencyOption(dep);
            _optionsByKey[GetOptionKey(option)] = option;
            items.Add(option);
        }

        foreach (var repo in repos.OrderBy(r => r.InterfaceName, StringComparer.OrdinalIgnoreCase))
        {
            var reference = new ArtifactRef(
                GeneratorNodeKind.Repository,
                ArtifactOrigin.Project,
                repo.InterfaceName,
                ProjectPath: repo.Path,
                DisplayName: repo.InterfaceName);
            var option = new CommandDependencyOption(repo.InterfaceName, false, reference);
            _optionsByKey[GetOptionKey(option)] = option;
            items.Add(option);
        }

        Picker.SetDiscovered(items);
    }

    public void SetDiscovered(IEnumerable<AvailableArtifactItem> artifacts)
    {
        var items = new List<object>();
        _optionsByKey.Clear();

        foreach (var dep in StandardDeps)
        {
            var option = new CommandDependencyOption(dep);
            _optionsByKey[GetOptionKey(option)] = option;
            items.Add(option);
        }

        foreach (var artifact in artifacts
            .Where(a => a.Kind == GeneratorNodeKind.Repository)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            var option = new CommandDependencyOption(artifact.Name, artifact.IsFromSession, artifact.Ref);
            _optionsByKey[GetOptionKey(option)] = option;
            items.Add(option);
        }

        Picker.SetDiscovered(items);
    }

    public void SetSelectedDependencies(IEnumerable<ArtifactRef> references)
    {
        SetSelectedDependencies(references, Array.Empty<string>());
    }

    public void SetSelectedDependencies(IEnumerable<ArtifactRef> references, IEnumerable<string> standardDependencyNames)
    {
        var selectedKeys = references
            .Select(reference => GetOptionKey(new CommandDependencyOption(reference.Name, reference.IsFromSession, reference)))
            .Concat(standardDependencyNames.Select(name => GetOptionKey(new CommandDependencyOption(name))))
            .ToHashSet(StringComparer.Ordinal);

        Picker.DeselectAllCommand.Execute(null);

        foreach (var item in Picker.FilteredItems.Cast<WrappedListItem>().ToList())
        {
            if (item.OriginalItem is CommandDependencyOption option && selectedKeys.Contains(GetOptionKey(option)))
            {
                Picker.ToggleItemCommand.Execute(item.OriginalItem);
            }
        }
    }

    public IReadOnlyList<CommandHandlerDependency> GetSelected()
    {
        return Picker.SelectedDisplayTexts
            .Select(name => new CommandHandlerDependency(name, GenerationNaming.ToDependencyName(name)))
            .ToList();
    }

    public IReadOnlyList<CommandDependencyOption> GetSelectedOptions()
    {
        return Picker.SelectedKeys
            .Select(key => _optionsByKey.GetValueOrDefault(key))
            .Where(option => option is not null)
            .Select(option => option!)
            .ToList();
    }

    private static string GetOptionKey(object? item)
    {
        if (item is not CommandDependencyOption option)
        {
            return item?.ToString() ?? string.Empty;
        }

        return option.Ref is not null
            ? ArtifactKey.From(option.Ref).Value
            : $"standard:{option.InterfaceName}";
    }
}
