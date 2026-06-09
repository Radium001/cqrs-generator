using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session;

public abstract class GeneratorDefinition<TState> : IGeneratorDefinition
    where TState : class
{
    public abstract GeneratorNodeKind Kind { get; }

    public abstract string DisplayName { get; }

    public abstract TState CreateInitialState(GeneratorCreationContext context);

    public abstract IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        TState state,
        GenerationSession session,
        GeneratorSessionServices services);

    public abstract GeneratorValidationResult Validate(
        GeneratorNode node,
        TState state,
        GenerationSession session);

    public abstract GeneratorPreview BuildPreview(
        GeneratorNode node,
        TState state,
        GenerationSession session);

    public abstract GenerationPlan BuildPlan(
        GeneratorNode node,
        TState state,
        GenerationSession session,
        CoreWorkflowContext core);

    object IGeneratorDefinition.CreateInitialState(GeneratorCreationContext context)
        => CreateInitialState(context);

    IGeneratorNodeEditorViewModel IGeneratorDefinition.CreateEditor(
        GeneratorNode node,
        GenerationSession session,
        GeneratorSessionServices services)
        => CreateEditor(node, (TState)node.State, session, services);

    GeneratorValidationResult IGeneratorDefinition.Validate(
        GeneratorNode node,
        GenerationSession session)
        => Validate(node, (TState)node.State, session);

    GeneratorPreview IGeneratorDefinition.BuildPreview(
        GeneratorNode node,
        GenerationSession session)
        => BuildPreview(node, (TState)node.State, session);

    GenerationPlan IGeneratorDefinition.BuildPlan(
        GeneratorNode node,
        GenerationSession session,
        CoreWorkflowContext core)
        => BuildPlan(node, (TState)node.State, session, core);
}
