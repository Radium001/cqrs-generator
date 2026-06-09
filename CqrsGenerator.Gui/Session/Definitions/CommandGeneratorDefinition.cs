using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class CommandGeneratorDefinition : GeneratorDefinition<CommandGeneratorState>
{
    public override GeneratorNodeKind Kind => GeneratorNodeKind.Command;

    public override string DisplayName => "Command";

    public override CommandGeneratorState CreateInitialState(GeneratorCreationContext context)
    {
        return new CommandGeneratorState { CommandName = "Create" };
    }

    public override IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        CommandGeneratorState state,
        GenerationSession session,
        GeneratorSessionServices services)
    {
        var planService = services.ServiceProvider.GetRequiredService<IAddCommandPlanService>();
        var scenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddCommandScenarioOutlineBuilder>();
        var repositoryPlanService = services.ServiceProvider.GetRequiredService<IAddRepositoryPlanService>();
        var repositoryScenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddRepositoryScenarioOutlineBuilder>();
        var entityPlanService = services.ServiceProvider.GetRequiredService<IAddEntityPlanService>();
        var entityScenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddEntityScenarioOutlineBuilder>();
        var efPreparationService = services.ServiceProvider.GetRequiredService<EfEntityPreparationService>();
        var vm = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            null!,
            planService,
            scenarioOutlineBuilder,
            repositoryPlanService,
            repositoryScenarioOutlineBuilder,
            entityPlanService,
            entityScenarioOutlineBuilder,
            efPreparationService,
            node: node);
        vm.SetGenerationSession(session, services.Navigator);
        return vm;
    }

    public override GeneratorValidationResult Validate(
        GeneratorNode node,
        CommandGeneratorState state,
        GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(state.CommandName))
            return GeneratorValidationResult.Error("Command name is required.");

        var danglingRepos = state.RepositoryNodeIds
            .Select(id => session.FindNode(id))
            .Where(n => n is null)
            .Count();

        if (danglingRepos > 0)
            return GeneratorValidationResult.Error($"{danglingRepos} referenced repository node(s) no longer exist.");

        return GeneratorValidationResult.Valid;
    }

    public override GeneratorPreview BuildPreview(
        GeneratorNode node,
        CommandGeneratorState state,
        GenerationSession session)
    {
        return new GeneratorPreview(node, $"Command: {state.CommandName}", [], [], [], []);
    }

    public override GenerationPlan BuildPlan(
        GeneratorNode node,
        CommandGeneratorState state,
        GenerationSession session,
        CoreWorkflowContext core)
    {
        var planService = core.ServiceProvider.GetRequiredService<IAddCommandPlanService>();

        var formState = new AddCommandFormState(
            null,
            state.FeaturePath,
            state.CommandName,
            state.ResponseType,
            state.Parameters
                .Select(p => new PropertySpec(p.Type, p.Name))
                .ToArray(),
            Array.Empty<CommandHandlerDependency>(),
            Array.Empty<object>(),
            false);

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }
}