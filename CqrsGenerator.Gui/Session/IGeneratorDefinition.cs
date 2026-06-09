using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session;

public interface IGeneratorDefinition
{
    GeneratorNodeKind Kind { get; }

    string DisplayName { get; }

    object CreateInitialState(GeneratorCreationContext context);

    IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        GenerationSession session,
        GeneratorSessionServices services);

    GeneratorValidationResult Validate(
        GeneratorNode node,
        GenerationSession session);

    GeneratorPreview BuildPreview(
        GeneratorNode node,
        GenerationSession session);

    GenerationPlan BuildPlan(
        GeneratorNode node,
        GenerationSession session,
        CoreWorkflowContext core);
}
