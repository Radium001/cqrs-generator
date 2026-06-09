using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CqrsGenerator.Gui.Session;

public sealed partial class GeneratorNode : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    public Guid? ParentId { get; init; }

    public GeneratorNodeKind Kind { get; init; }

    public string? RelationshipName { get; init; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private GeneratorNodeStatus _status = GeneratorNodeStatus.Draft;

    [ObservableProperty]
    private GeneratorNodeLifecycle _lifecycle = GeneratorNodeLifecycle.Draft;

    public object State { get; set; } = default!;

    public ObservableCollection<GeneratorNode> Children { get; } = new();

    public IEnumerable<GeneratorNode> Traverse()
    {
        yield return this;

        foreach (var child in Children)
        {
            foreach (var node in child.Traverse())
            {
                yield return node;
            }
        }
    }
}
