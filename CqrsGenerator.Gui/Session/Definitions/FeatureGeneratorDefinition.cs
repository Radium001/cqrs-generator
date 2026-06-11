using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class FeatureGeneratorDefinition : GeneratorDefinition<FeatureGeneratorState>
{
    public override GeneratorNodeKind Kind => GeneratorNodeKind.Feature;

    public override string DisplayName => "Feature";

    public override FeatureGeneratorState CreateInitialState(GeneratorCreationContext context)
    {
        return new FeatureGeneratorState { FeatureName = "NewFeature", CreateWebFeature = true };
    }

    public override IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        FeatureGeneratorState state,
        GenerationSession session,
        GeneratorSessionServices services)
    {
        var planService = services.ServiceProvider.GetRequiredService<ICreateFeaturePlanService>();
        var scenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<ICreateFeatureScenarioOutlineBuilder>();
        return new CreateFeatureRootSessionViewModel(
            actionDescriptor: null,
            planService,
            scenarioOutlineBuilder,
            isStandalone: false,
            node: node);
    }

    public override GeneratorValidationResult Validate(
        GeneratorNode node,
        FeatureGeneratorState state,
        GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(state.FeatureName))
            return GeneratorValidationResult.Error("Feature name is required.");

        return GeneratorValidationResult.Valid;
    }

    public override GeneratorPreview BuildPreview(
        GeneratorNode node,
        FeatureGeneratorState state,
        GenerationSession session)
    {
        return new GeneratorPreview(node, $"Feature: {state.FeatureName}", [], [], [], []);
    }

    public override GenerationPlan BuildPlan(
        GeneratorNode node,
        FeatureGeneratorState state,
        GenerationSession session,
        CoreWorkflowContext core)
    {
        var planService = core.ServiceProvider.GetRequiredService<ICreateFeaturePlanService>();

        var formState = new CreateFeatureFormState(
            state.FeatureName,
            state.Subfolder,
            state.CreateWebFeature);

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }
}
