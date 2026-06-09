using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class DtoGeneratorDefinition : GeneratorDefinition<DtoGeneratorState>
{
    public override GeneratorNodeKind Kind => GeneratorNodeKind.Dto;

    public override string DisplayName => "Dto";

    public override DtoGeneratorState CreateInitialState(GeneratorCreationContext context)
    {
        return new DtoGeneratorState { BaseName = "NewDto" };
    }

    public override IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        DtoGeneratorState state,
        GenerationSession session,
        GeneratorSessionServices services)
    {
        var planService = services.ServiceProvider.GetRequiredService<IAddDtoPlanService>();
        var scenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddDtoScenarioOutlineBuilder>();
        return new DtoRootSessionViewModel(
            actionDescriptor: null,
            planService,
            null!,
            scenarioOutlineBuilder,
            isStandalone: false,
            node: node);
    }

    public override GeneratorValidationResult Validate(
        GeneratorNode node,
        DtoGeneratorState state,
        GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(state.BaseName))
            return GeneratorValidationResult.Error("DTO name is required.");

        return GeneratorValidationResult.Valid;
    }

    public override GeneratorPreview BuildPreview(
        GeneratorNode node,
        DtoGeneratorState state,
        GenerationSession session)
    {
        return new GeneratorPreview(node, $"Dto: {state.BaseName}", [], [], [], []);
    }

    public override GenerationPlan BuildPlan(
        GeneratorNode node,
        DtoGeneratorState state,
        GenerationSession session,
        CoreWorkflowContext core)
    {
        var planService = core.ServiceProvider.GetRequiredService<IAddDtoPlanService>();

        var formState = new AddDtoFormState(
            null,
            null,
            state.BaseName,
            state.Properties
                .Select(p => new PropertySpec(p.Type, p.Name))
                .ToArray(),
            false,
            null);

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }
}
