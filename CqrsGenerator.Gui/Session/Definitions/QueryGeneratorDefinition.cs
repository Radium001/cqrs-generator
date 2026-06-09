using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class QueryGeneratorDefinition : GeneratorDefinition<QueryGeneratorState>
{
    public override GeneratorNodeKind Kind => GeneratorNodeKind.Query;

    public override string DisplayName => "Query";

    public override QueryGeneratorState CreateInitialState(GeneratorCreationContext context)
    {
        return new QueryGeneratorState { QueryName = "Get" };
    }

    public override IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        QueryGeneratorState state,
        GenerationSession session,
        GeneratorSessionServices services)
    {
        var planService = services.ServiceProvider.GetRequiredService<IAddQueryPlanService>();
        var addDtoPlanService = services.ServiceProvider.GetRequiredService<IAddDtoPlanService>();
        var addDtoScenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddDtoScenarioOutlineBuilder>();
        var queryServiceSuggestionService = services.ServiceProvider.GetRequiredService<IQueryServiceSuggestionService>();
        var scenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddQueryScenarioOutlineBuilder>();
        var createFeaturePlanService = services.ServiceProvider.GetRequiredService<ICreateFeaturePlanService>();
        var createFeatureScenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<ICreateFeatureScenarioOutlineBuilder>();
        var vm = new AddQueryRootSessionViewModel(
            actionDescriptor: null,
            null!,
            planService,
            addDtoPlanService,
            addDtoScenarioOutlineBuilder,
            queryServiceSuggestionService,
            scenarioOutlineBuilder,
            createFeaturePlanService: createFeaturePlanService,
            createFeatureScenarioOutlineBuilder: createFeatureScenarioOutlineBuilder,
            node: node);
        vm.SetGenerationSession(session, services.Navigator);
        return vm;
    }

    public override GeneratorValidationResult Validate(
        GeneratorNode node,
        QueryGeneratorState state,
        GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(state.QueryName))
            return GeneratorValidationResult.Error("Query name is required.");

        if (state.ResultDtoNodeId.HasValue && session.FindNode(state.ResultDtoNodeId.Value) is null)
            return GeneratorValidationResult.Error("Referenced DTO node no longer exists.");

        return GeneratorValidationResult.Valid;
    }

    public override GeneratorPreview BuildPreview(
        GeneratorNode node,
        QueryGeneratorState state,
        GenerationSession session)
    {
        return new GeneratorPreview(node, $"Query: {state.QueryName}", [], [], [], []);
    }

    public override GenerationPlan BuildPlan(
        GeneratorNode node,
        QueryGeneratorState state,
        GenerationSession session,
        CoreWorkflowContext core)
    {
        var planService = core.ServiceProvider.GetRequiredService<IAddQueryPlanService>();

        var dtoName = GetDtoName(state, session);
        var formState = new AddQueryFormState(
            featureName: null,
            state.FeaturePath,
            state.QueryName,
            dtoName,
            state.ResultDtoNodeId.HasValue,
            null,
            ResponseShape.Single,
            state.Parameters.ToArray(),
            null,
            false,
            string.Empty,
            true,
            false,
            false);

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }

    private static string? GetDtoName(QueryGeneratorState state, GenerationSession session)
    {
        if (state.ResultDtoNodeId is null)
            return state.ExistingResultDtoName;

        var dtoNode = session.FindNode(state.ResultDtoNodeId.Value);
        if (dtoNode?.State is DtoGeneratorState dtoState)
        {
            var suffix = dtoState.SuffixIndex >= 0 && dtoState.SuffixIndex < 2
                ? new[] { "", "Dto" }[dtoState.SuffixIndex]
                : string.Empty;
            return dtoState.BaseName + suffix;
        }

        return null;
    }
}