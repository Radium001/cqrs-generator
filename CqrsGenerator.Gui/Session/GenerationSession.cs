using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CqrsGenerator.Gui.Session;

public sealed partial class GenerationSession : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    public ObservableCollection<GeneratorNode> Roots { get; } = new();

    public SessionArtifactIndex Artifacts { get; }

    // Legacy compatibility only. Runtime code uses typed state + Relations.
    public ReferenceRegistry References { get; } = new();

    public GenerationSessionRelationIndex Relations { get; }

    [ObservableProperty]
    private GeneratorNode? _activeNode;

    public GenerationSession()
    {
        Artifacts = new SessionArtifactIndex(this);
        Relations = new GenerationSessionRelationIndex(this);
    }

    public IEnumerable<GeneratorNode> Traverse()
    {
        foreach (var root in Roots)
        {
            foreach (var node in root.Traverse())
            {
                yield return node;
            }
        }
    }

    public GeneratorNode? FindNode(Guid id)
    {
        return Traverse().FirstOrDefault(x => x.Id == id);
    }

    public void Reset()
    {
        Roots.Clear();
        ActiveNode = null;
    }
}
