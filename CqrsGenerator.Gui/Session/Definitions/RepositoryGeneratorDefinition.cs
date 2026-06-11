using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui.Session.Definitions;

public sealed class RepositoryGeneratorDefinition : GeneratorDefinition<RepositoryGeneratorState>
{
    public override GeneratorNodeKind Kind => GeneratorNodeKind.Repository;

    public override string DisplayName => "Repository";

    public override RepositoryGeneratorState CreateInitialState(GeneratorCreationContext context)
    {
        return new RepositoryGeneratorState { InterfaceName = "IRepository" };
    }

    public override IGeneratorNodeEditorViewModel CreateEditor(
        GeneratorNode node,
        RepositoryGeneratorState state,
        GenerationSession session,
        GeneratorSessionServices services)
    {
        var planService = services.ServiceProvider.GetRequiredService<IAddRepositoryPlanService>();
        var scenarioOutlineBuilder = services.ServiceProvider.GetRequiredService<IAddRepositoryScenarioOutlineBuilder>();
        var vm = new RepositoryRootSessionViewModel(
            actionDescriptor: null,
            planService,
            scenarioOutlineBuilder,
            null!,
            null!,
            null!,
            isStandalone: false,
            node: node);
        vm.SetGenerationSession(session, services.Navigator);
        return vm;
    }

    public override GeneratorValidationResult Validate(
        GeneratorNode node,
        RepositoryGeneratorState state,
        GenerationSession session)
    {
        if (string.IsNullOrWhiteSpace(state.InterfaceName))
            return GeneratorValidationResult.Error("Repository interface name is required.");

        if (state.EntityRef?.IsFromSession == true &&
            state.EntityRef.NodeId.HasValue &&
            session.FindNode(state.EntityRef.NodeId.Value) is null)
            return GeneratorValidationResult.Error("Referenced entity node no longer exists.");

        return GeneratorValidationResult.Valid;
    }

    public override GeneratorPreview BuildPreview(
        GeneratorNode node,
        RepositoryGeneratorState state,
        GenerationSession session)
    {
        return new GeneratorPreview(node, $"Repository: {state.InterfaceName}", [], [], [], []);
    }

    public override GenerationPlan BuildPlan(
        GeneratorNode node,
        RepositoryGeneratorState state,
        GenerationSession session,
        CoreWorkflowContext core)
    {
        var planService = core.ServiceProvider.GetRequiredService<IAddRepositoryPlanService>();

        var entity = GetEntity(state, session);
        var entityName = entity?.Name;
        var entityNamespace = entity?.Namespace;

        var formState = new AddRepositoryFormState(
            entityName,
            entityName,
            entityNamespace,
            null,
            state.SelectedMethodPresetKeys.ToArray(),
            state.CustomMethods.ToArray(),
            state.AddDependencyInjectionRegistration);

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }

    private static (string Name, string Namespace)? GetEntity(RepositoryGeneratorState state, GenerationSession session)
    {
        if (state.EntityRef is null)
            return null;

        if (state.EntityRef.IsFromProject || !state.EntityRef.NodeId.HasValue)
        {
            return (state.EntityRef.Name, state.EntityRef.Namespace ?? GeneratorConstants.DomainEntitiesNamespace);
        }

        var entityNode = session.FindNode(state.EntityRef.NodeId.Value);
        if (entityNode?.State is EntityGeneratorState entityState)
        {
            return (entityState.EntityName, state.EntityRef.Namespace ?? GeneratorConstants.DomainEntitiesNamespace);
        }

        return (state.EntityRef.Name, state.EntityRef.Namespace ?? GeneratorConstants.DomainEntitiesNamespace);
    }
}
