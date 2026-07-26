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
        var vm = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            planService,
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

        var danglingRepos = state.RepositoryRefs
            .Where(reference => reference.IsFromSession && reference.NodeId.HasValue)
            .Select(reference => session.FindNode(reference.NodeId!.Value))
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

        var dependencyNames = state.StandardDependencyNames
            .Concat(state.RepositoryRefs.Select(reference => reference.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var dependencies = dependencyNames
            .Select(name => new CommandHandlerDependency(name, GenerationNaming.ToDependencyName(name)))
            .ToArray();

        var formState = new AddCommandFormState(
            state.FeatureRef?.DisplayName ?? state.FeatureRef?.Name,
            state.FeatureRef?.FeaturePath,
            state.CommandName,
            state.Parameters
                .Select(p => new PropertySpec(p.Type, p.Name))
                .ToArray(),
            dependencies,
            state.WebFeatureRef?.FeaturePath)
        {
            GenerateHandlerBody = state.GenerateHandlerBody,
            HandlerScaffoldContext = BuildScaffoldContext(state, session, core.WorkspaceContext.ProjectModel),
        };

        return planService.BuildPlan(core.WorkspaceContext, formState);
    }

    private static CommandHandlerScaffoldContext BuildScaffoldContext(
        CommandGeneratorState command,
        GenerationSession session,
        ProjectModel project)
    {
        var repositories = command.RepositoryRefs
            .Select(reference => ResolveRepository(reference, session, project))
            .Where(contract => contract is not null)
            .Select(contract => contract!)
            .ToArray();
        return new CommandHandlerScaffoldContext(repositories);
    }

    private static CommandHandlerRepositoryContract? ResolveRepository(
        ArtifactRef reference,
        GenerationSession session,
        ProjectModel project)
    {
        if (reference.IsFromProject)
        {
            var repository = project.Repositories.FirstOrDefault(candidate =>
                string.Equals(candidate.InterfaceName, reference.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(reference.ProjectPath) &&
                 string.Equals(candidate.Path, reference.ProjectPath, StringComparison.OrdinalIgnoreCase)));
            if (repository is null)
            {
                return null;
            }

            return new CommandHandlerRepositoryContract(
                repository.InterfaceName,
                GenerationNaming.ToDependencyName(repository.InterfaceName),
                repository.EntityName,
                repository.Methods,
                ResolveProjectEntity(repository.EntityName, project));
        }

        if (reference.NodeId is not Guid nodeId ||
            session.FindNode(nodeId)?.State is not RepositoryGeneratorState repositoryState)
        {
            return null;
        }

        var entityName = repositoryState.EntityRef?.Name;
        var selectedPresetKeys = repositoryState.SelectedMethodPresetKeys.Count == 0
            ? RepositoryMethodCatalog.Presets
                .Where(preset => preset.IsSelectedByDefault)
                .Select(preset => preset.Key)
            : repositoryState.SelectedMethodPresetKeys;
        var methods = selectedPresetKeys
            .Select(key => RepositoryMethodCatalog.Create(key, entityName ?? "Entity"))
            .Concat(repositoryState.CustomMethods)
            .Select(ToHandlerMethodContract)
            .ToArray();

        return new CommandHandlerRepositoryContract(
            repositoryState.InterfaceName,
            GenerationNaming.ToDependencyName(repositoryState.InterfaceName),
            entityName,
            methods,
            ResolveEntity(repositoryState.EntityRef, session, project));
    }

    private static CommandHandlerMethodContract ToHandlerMethodContract(RepositoryMethodSpec method)
    {
        var parameters = method.Parameters.Any(parameter =>
                string.Equals(parameter.Type, GeneratorConstants.CancellationTokenType, StringComparison.Ordinal))
            ? method.Parameters
            : method.Parameters
                .Append(new PropertySpec(
                    GeneratorConstants.CancellationTokenType,
                    GeneratorConstants.CancellationTokenParamName))
                .ToArray();
        return new CommandHandlerMethodContract(method.Name, method.ReturnType, parameters);
    }

    private static CommandHandlerEntityContract? ResolveEntity(
        ArtifactRef? reference,
        GenerationSession session,
        ProjectModel project)
    {
        if (reference is null)
        {
            return null;
        }

        if (reference.IsFromProject)
        {
            return ResolveProjectEntity(reference.Name, project);
        }

        if (reference.NodeId is not Guid nodeId ||
            session.FindNode(nodeId)?.State is not EntityGeneratorState entityState)
        {
            return null;
        }

        var methods = new List<CommandHandlerMethodContract>();
        if (entityState.GenerateFactoryMethod)
        {
            methods.Add(new CommandHandlerMethodContract(
                "Create",
                entityState.EntityName,
                entityState.Properties.ToArray(),
                IsStatic: true));
        }

        methods.AddRange(entityState.DomainMethods.Select(name =>
            new CommandHandlerMethodContract(name, "void", [])));

        var entityNamespace = string.IsNullOrWhiteSpace(entityState.Subfolder)
            ? GeneratorConstants.DomainEntitiesNamespace
            : $"{GeneratorConstants.DomainEntitiesNamespace}.{entityState.Subfolder.Replace('/', '.').Replace('\\', '.')}";
        return new CommandHandlerEntityContract(entityState.EntityName, entityNamespace, methods);
    }

    private static CommandHandlerEntityContract? ResolveProjectEntity(string? entityName, ProjectModel project)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return null;
        }

        var entity = project.Entities.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, entityName, StringComparison.OrdinalIgnoreCase));
        return entity is null
            ? null
            : new CommandHandlerEntityContract(entity.Name, entity.Namespace, entity.Methods);
    }
}
