using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class QueryGeneratorDefinition : GeneratorDefinition<QueryGeneratorState>
{
    private const string QueryServiceUnavailableMessage = "Query service is not available.";
    private const string QueryServiceBlockedMessage = "Query service implementation could not be resolved automatically. Normalize the existing implementation placement before adding methods through this flow.";
    private static readonly IQueryServiceSuggestionService FallbackQueryServiceSuggestionService = new QueryServiceSuggestionService();

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

        if (state.ResultDtoRef?.IsFromSession == true &&
            state.ResultDtoRef.NodeId.HasValue &&
            session.FindNode(state.ResultDtoRef.NodeId.Value) is null)
            return GeneratorValidationResult.Error("Referenced DTO node no longer exists.");

        if (state.CreateQueryServiceMethod)
        {
            var queryServiceSuggestion = ResolveQueryServiceSuggestion(state, session, session.Artifacts.ProjectModel, FallbackQueryServiceSuggestionService);
            if (queryServiceSuggestion is null)
            {
                return GeneratorValidationResult.Error(QueryServiceUnavailableMessage);
            }

            if (queryServiceSuggestion.Mode == QueryServiceSuggestionMode.Blocked)
            {
                return GeneratorValidationResult.Error(QueryServiceBlockedMessage);
            }
        }

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
        var queryServiceSuggestionService = core.ServiceProvider.GetRequiredService<IQueryServiceSuggestionService>();

        var dtoName = GetDtoName(state, session);
        var queryServiceSuggestion = ResolveQueryServiceSuggestion(
            state,
            session,
            core.WorkspaceContext.ProjectModel,
            queryServiceSuggestionService);
        var formState = new AddQueryFormState(
            featureName: GetFeatureMetadata(state, session).FeatureName,
            state.FeatureRef?.FeaturePath,
            state.QueryName,
            dtoName,
            state.ResultDtoRef?.IsFromSession == true,
            null,
            state.ResponseShape,
            state.Parameters.ToArray(),
            queryServiceSuggestion,
            state.CreateQueryServiceMethod,
            state.MethodName,
            state.GenerateHandlerBody,
            state.GenerateQueryServiceBody,
            state.UpdateWebImports);

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }

    private static QueryServiceSuggestion? ResolveQueryServiceSuggestion(
        QueryGeneratorState state,
        GenerationSession session,
        CqrsGenerator.Core.Discovery.ProjectModel? projectModel,
        IQueryServiceSuggestionService queryServiceSuggestionService)
    {
        if (!state.CreateQueryServiceMethod || state.FeatureRef is null)
        {
            return null;
        }

        var (featureName, featurePath) = GetFeatureMetadata(state, session);
        return queryServiceSuggestionService.Suggest(projectModel, featureName, featurePath);
    }

    private static (string? FeatureName, string? FeaturePath) GetFeatureMetadata(QueryGeneratorState state, GenerationSession session)
    {
        if (state.FeatureRef is null)
        {
            return (null, null);
        }

        var feature = session.Artifacts.Find(state.FeatureRef);
        if (feature is not null)
        {
            return (feature.Name, feature.FeaturePath);
        }

        return (state.FeatureRef.DisplayName ?? state.FeatureRef.Name, state.FeatureRef.FeaturePath);
    }

    private static string? GetDtoName(QueryGeneratorState state, GenerationSession session)
    {
        if (state.ResultDtoRef is null)
            return state.ExistingResultDtoName;

        if (state.ResultDtoRef.IsFromProject || !state.ResultDtoRef.NodeId.HasValue)
        {
            return state.ResultDtoRef.Name;
        }

        var dtoNode = session.FindNode(state.ResultDtoRef.NodeId.Value);
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
