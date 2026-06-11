using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
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

        if (state.FeatureRef is null)
            return GeneratorValidationResult.Error("Feature is required.");

        var resultDtoRef = state.ResultDtoRef;
        if (resultDtoRef is null && string.IsNullOrWhiteSpace(state.CustomDtoName))
            return GeneratorValidationResult.Error("Result DTO is required.");

        if (resultDtoRef?.IsFromSession == true &&
            resultDtoRef.NodeId.HasValue &&
            session.FindNode(resultDtoRef.NodeId.Value) is null)
            return GeneratorValidationResult.Error("Referenced DTO node no longer exists.");

        if (state.CreateQueryServiceMethod)
        {
            var queryServiceSuggestion = ResolveQueryServiceSuggestion(state, session, session.Artifacts.ProjectModel, FallbackQueryServiceSuggestionService, node);
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

        var featureRef = state.FeatureRef;
        var dtoSelection = BuildDtoSelection(state, session, node);
        var dtoProperties = GetDtoProperties(state, session, node);
        var queryServiceSuggestion = ResolveQueryServiceSuggestion(
            state,
            session,
            core.WorkspaceContext.ProjectModel,
            queryServiceSuggestionService,
            node);
        var formState = new AddQueryFormState(
            GetFeatureMetadata(state, session, node).FeatureName,
            featureRef?.FeaturePath,
            state.QueryName,
            dtoSelection,
            dtoProperties,
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
        IQueryServiceSuggestionService queryServiceSuggestionService,
        GeneratorNode node)
    {
        var featureRef = state.FeatureRef;
        if (!state.CreateQueryServiceMethod || featureRef is null)
        {
            return null;
        }

        var (featureName, featurePath) = GetFeatureMetadata(state, session, node);
        return queryServiceSuggestionService.Suggest(projectModel, featureName, featurePath);
    }

    private static (string? FeatureName, string? FeaturePath) GetFeatureMetadata(QueryGeneratorState state, GenerationSession session, GeneratorNode node)
    {
        var featureRef = state.FeatureRef;
        if (featureRef is null)
        {
            return (null, null);
        }

        var feature = session.Artifacts.Find(featureRef);
        if (feature is not null)
        {
            return (feature.Name, feature.FeaturePath);
        }

        return (featureRef.DisplayName ?? featureRef.Name, featureRef.FeaturePath);
    }

    private static IReadOnlyList<PropertySpec>? GetDtoProperties(QueryGeneratorState state, GenerationSession session, GeneratorNode node)
    {
        var resultDtoRef = state.ResultDtoRef;
        if (resultDtoRef?.NodeId is Guid nodeId)
        {
            var dtoNode = session.FindNode(nodeId);
            if (dtoNode?.State is DtoGeneratorState dtoState && dtoState.Properties.Count > 0)
                return dtoState.Properties.ToArray();
        }
        return null;
    }

    private static QueryDtoSelectionState? BuildDtoSelection(QueryGeneratorState state, GenerationSession session, GeneratorNode node)
    {
        var resultDtoRef = state.ResultDtoRef;
        if (resultDtoRef is null)
        {
            return string.IsNullOrWhiteSpace(state.CustomDtoName)
                ? null
                : new QueryDtoSelectionState(
                    state.CustomDtoName.Trim(),
                    null,
                    null,
                    DtoLocationKind.SharedFeatureDto,
                    null,
                    CreateNewLocalDto: false,
                    IsSelectable: true,
                    SelectionBlockedReason: null);
        }

        if (resultDtoRef.NodeId is Guid nodeId)
        {
            var dtoNode = session.FindNode(nodeId);
            var dtoName = dtoNode?.State is DtoGeneratorState dtoState
                ? GetDtoName(dtoState)
                : resultDtoRef.Name;

            if (IsOwnedResultDto(node, nodeId))
            {
                return new QueryDtoSelectionState(
                    dtoName,
                    null,
                    null,
                    DtoLocationKind.LocalQueryDto,
                    StringUtilities.StripSuffix(state.QueryName, GeneratorConstants.QuerySuffix),
                    CreateNewLocalDto: true,
                    IsSelectable: true,
                    SelectionBlockedReason: null);
            }

            return new QueryDtoSelectionState(
                dtoName,
                resultDtoRef.Namespace,
                resultDtoRef.ProjectPath,
                string.IsNullOrWhiteSpace(resultDtoRef.OwnerName) ? DtoLocationKind.SharedFeatureDto : DtoLocationKind.LocalQueryDto,
                resultDtoRef.OwnerName,
                CreateNewLocalDto: false,
                IsSelectable: true,
                SelectionBlockedReason: null);
        }

        return new QueryDtoSelectionState(
            resultDtoRef.Name,
            resultDtoRef.Namespace,
            resultDtoRef.ProjectPath,
            string.IsNullOrWhiteSpace(resultDtoRef.OwnerName) ? DtoLocationKind.SharedFeatureDto : DtoLocationKind.LocalQueryDto,
            resultDtoRef.OwnerName,
            CreateNewLocalDto: false,
            IsSelectable: true,
            SelectionBlockedReason: null);
    }

    private static bool IsOwnedResultDto(GeneratorNode queryNode, Guid dtoNodeId)
    {
        return queryNode.Children.Any(child =>
            child.Id == dtoNodeId &&
            child.Kind == GeneratorNodeKind.Dto &&
            string.Equals(child.RelationshipName, "ResultDto", StringComparison.Ordinal));
    }

    private static string GetDtoName(DtoGeneratorState dtoState)
    {
        var suffix = dtoState.SuffixIndex == 1 ? GeneratorConstants.DtoSuffix : string.Empty;
        return dtoState.BaseName + suffix;
    }
}
