using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class WebPageGeneratorDefinition : GeneratorDefinition<WebPageGeneratorState>
{
    public override GeneratorNodeKind Kind => GeneratorNodeKind.WebPage;

    public override string DisplayName => "Web Page";

    public override WebPageGeneratorState CreateInitialState(GeneratorCreationContext context)
    {
        return new WebPageGeneratorState { PageName = "NewPage" };
    }

    public override IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        WebPageGeneratorState state,
        GenerationSession session,
        GeneratorSessionServices services)
    {
        var webPagePlanService = services.ServiceProvider.GetRequiredService<IAddWebPagePlanService>();
        var queryPlanService = services.ServiceProvider.GetRequiredService<IAddQueryPlanService>();
        var queryScenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddQueryScenarioOutlineBuilder>();
        var scenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddWebPageScenarioOutlineBuilder>();
        var queryServiceSuggestionService = services.ServiceProvider.GetRequiredService<IQueryServiceSuggestionService>();
        var vm = new AddWebPageRootSessionViewModel(
            new GenerationActionDescriptor("add-web-page", "Add Web Page", "UI", "Ready", true),
            webPagePlanService,
            queryPlanService,
            queryScenarioOutlineBuilder,
            scenarioOutlineBuilder,
            queryServiceSuggestionService,
            node: node);
        vm.SetGenerationSession(session, services.Navigator);
        return vm;
    }

    public override GeneratorValidationResult Validate(
        GeneratorNode node,
        WebPageGeneratorState state,
        GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(state.PageName))
            return GeneratorValidationResult.Error("Page name is required.");

        var danglingQueries = state.QueryRefs
            .Where(reference => reference.IsFromSession && reference.NodeId.HasValue)
            .Select(reference => session.FindNode(reference.NodeId!.Value))
            .Where(n => n is null)
            .Count();

        if (danglingQueries > 0)
            return GeneratorValidationResult.Error($"{danglingQueries} referenced query node(s) no longer exist.");

        return GeneratorValidationResult.Valid;
    }

    public override GeneratorPreview BuildPreview(
        GeneratorNode node,
        WebPageGeneratorState state,
        GenerationSession session)
    {
        return new GeneratorPreview(node, $"Web Page: {state.PageName}", [], [], [], []);
    }

    public override GenerationPlan BuildPlan(
        GeneratorNode node,
        WebPageGeneratorState state,
        GenerationSession session,
        CoreWorkflowContext core)
    {
        var planService = core.ServiceProvider.GetRequiredService<IAddWebPagePlanService>();

        var formState = new AddWebPageFormState(
            state.FeatureRef?.FeaturePath,
            null,
            state.PageName,
            state.Route,
            false,
            Array.Empty<WebPageQueryBindingState>(),
            Array.Empty<AddQueryFormState>());

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }
}
