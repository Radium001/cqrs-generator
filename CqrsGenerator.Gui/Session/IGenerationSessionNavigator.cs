namespace CqrsGenerator.Gui.Session;

public interface IGenerationSessionNavigator
{
    GeneratorNode CreateRoot(GeneratorNodeKind kind, object state);

    GeneratorNode CreateChild(GeneratorNode parent, GeneratorNodeKind kind, object state);

    void OpenNode(Guid nodeId);

    void OpenParent();

    bool RemoveNode(Guid nodeId);

    IReadOnlyList<NodeUsage> FindUsages(Guid nodeId);
}
