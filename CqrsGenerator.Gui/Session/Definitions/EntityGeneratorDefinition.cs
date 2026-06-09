using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class EntityGeneratorDefinition : GeneratorDefinition<EntityGeneratorState>
{
    public override GeneratorNodeKind Kind => GeneratorNodeKind.Entity;

    public override string DisplayName => "Entity";

    public override EntityGeneratorState CreateInitialState(GeneratorCreationContext context)
    {
        return new EntityGeneratorState { EntityName = "NewEntity" };
    }

    public override IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        EntityGeneratorState state,
        GenerationSession session,
        GeneratorSessionServices services)
    {
        var planService = services.ServiceProvider.GetRequiredService<IAddEntityPlanService>();
        var scenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddEntityScenarioOutlineBuilder>();
        var efPreparationService = services.ServiceProvider.GetRequiredService<EfEntityPreparationService>();
        return new EntityRootSessionViewModel(
            actionDescriptor: null,
            planService,
            scenarioOutlineBuilder,
            efPreparationService,
            isStandalone: false,
            node: node);
    }

    public override GeneratorValidationResult Validate(
        GeneratorNode node,
        EntityGeneratorState state,
        GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(state.EntityName))
            return GeneratorValidationResult.Error("Entity name is required.");

        return GeneratorValidationResult.Valid;
    }

    public override GeneratorPreview BuildPreview(
        GeneratorNode node,
        EntityGeneratorState state,
        GenerationSession session)
    {
        return new GeneratorPreview(node, $"Entity: {state.EntityName}", [], [], [], []);
    }

    public override GenerationPlan BuildPlan(
        GeneratorNode node,
        EntityGeneratorState state,
        GenerationSession session,
        CoreWorkflowContext core)
    {
        var planService = core.ServiceProvider.GetRequiredService<IAddEntityPlanService>();

        var formState = new AddEntityFormState(
            EntitySourceMode.Manual,
            null,
            Array.Empty<string>(),
            false,
            state.EntityName,
            null,
            state.Properties
                .Select(p => new PropertySpec(p.Type, p.Name))
                .ToArray(),
            false,
            false,
            false,
            Array.Empty<string>());

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }
}
