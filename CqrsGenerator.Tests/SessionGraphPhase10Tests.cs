using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.Definitions;
using CqrsGenerator.Gui.Session.States;

namespace CqrsGenerator.Tests;

public sealed class SessionGraphPhase10Tests
{
    private static (GenerationSession Session, IGenerationSessionNavigator Navigator) CreateSessionAndNavigator()
    {
        var session = new GenerationSession();
        var navigator = new GenerationSessionNavigator(session);
        return (session, navigator);
    }

    private static ProjectModel CreateProjectModel()
    {
        return new ProjectModel
        {
            Paths = new ProjectPaths("", "", "", "", "", "", "", "", ""),
            Features = [new FeatureInfo("Users", "Users", "")],
            Dtos = [new DtoInfo("UserDto", "Users", "", "", "")],
            Repositories = [new RepositoryInfo("IUserRepository", "")],
            Entities = [new EntityInfo("User", "", "", "", null)],
            QueryServices = [],
            DependencyInjection = new DependencyInjectionInfo("", []),
        };
    }

    // ===== Session Graph Tests =====

    [Fact]
    public void GenerationSession_CanAddRootNodes()
    {
        var (session, navigator) = CreateSessionAndNavigator();

        var node = navigator.CreateRoot(GeneratorNodeKind.Command, new CommandGeneratorState());

        Assert.Contains(node, session.Roots);
        Assert.Single(session.Roots);
    }

    [Fact]
    public void GeneratorNode_Traverse_VisitsSelfAndAllChildrenRecursively()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var root = navigator.CreateRoot(GeneratorNodeKind.Command, new CommandGeneratorState());
        var child = navigator.CreateChild(root, GeneratorNodeKind.Repository, new RepositoryGeneratorState());
        var grandchild = navigator.CreateChild(child, GeneratorNodeKind.Entity, new EntityGeneratorState());

        var allNodes = root.Traverse().ToList();

        Assert.Equal(3, allNodes.Count);
        Assert.Contains(root, allNodes);
        Assert.Contains(child, allNodes);
        Assert.Contains(grandchild, allNodes);
    }

    [Fact]
    public void GeneratorNode_Traverse_WithNoChildren_ReturnsOnlySelf()
    {
        var node = new GeneratorNode { Kind = GeneratorNodeKind.Dto, Title = "Test", State = new DtoGeneratorState() };

        var allNodes = node.Traverse().ToList();

        Assert.Single(allNodes);
        Assert.Equal(node, allNodes[0]);
    }

    [Fact]
    public void GenerationSession_FindNode_ReturnsCorrectNodeById()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var root = navigator.CreateRoot(GeneratorNodeKind.Command, new CommandGeneratorState());

        var found = session.FindNode(root.Id);

        Assert.NotNull(found);
        Assert.Equal(root.Id, found.Id);
    }

    [Fact]
    public void GenerationSession_FindNode_ReturnsNullForUnknownId()
    {
        var (session, _) = CreateSessionAndNavigator();

        var found = session.FindNode(Guid.NewGuid());

        Assert.Null(found);
    }

    // ===== Navigation Tests =====

    [Fact]
    public void Navigator_CreateRoot_AddsToRoots()
    {
        var (session, navigator) = CreateSessionAndNavigator();

        var node = navigator.CreateRoot(GeneratorNodeKind.Query, new QueryGeneratorState());

        Assert.Single(session.Roots);
        Assert.Equal(node, session.Roots[0]);
        Assert.Equal(GeneratorNodeKind.Query, node.Kind);
        Assert.Null(node.ParentId);
    }

    [Fact]
    public void Navigator_CreateChild_AddsToParentChildrenAndSetsParentId()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var parent = navigator.CreateRoot(GeneratorNodeKind.Repository, new RepositoryGeneratorState());

        var child = navigator.CreateChild(parent, GeneratorNodeKind.Entity, new EntityGeneratorState());

        Assert.Single(parent.Children);
        Assert.Equal(child, parent.Children[0]);
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public void Navigator_OpenNode_SetsActiveNode()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var node = navigator.CreateRoot(GeneratorNodeKind.Dto, new DtoGeneratorState());

        var opened = navigator.OpenNode(node.Id);

        Assert.True(opened);
        Assert.Equal(node, session.ActiveNode);
    }

    [Fact]
    public void Navigator_OpenParent_NavigatesToParent()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var parent = navigator.CreateRoot(GeneratorNodeKind.Repository, new RepositoryGeneratorState());
        var child = navigator.CreateChild(parent, GeneratorNodeKind.Entity, new EntityGeneratorState());

        navigator.OpenNode(child.Id);
        navigator.OpenParent();

        Assert.Equal(parent.Id, session.ActiveNode?.Id);
    }

    [Fact]
    public void Navigator_OpenParent_DoesNothingForRootNode()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var root = navigator.CreateRoot(GeneratorNodeKind.Command, new CommandGeneratorState());
        navigator.OpenNode(root.Id);

        navigator.OpenParent();

        Assert.Equal(root, session.ActiveNode);
    }

    [Fact]
    public void Navigator_RemoveNode_RemovesFromParentChildren()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var parent = navigator.CreateRoot(GeneratorNodeKind.Repository, new RepositoryGeneratorState());
        var child = navigator.CreateChild(parent, GeneratorNodeKind.Entity, new EntityGeneratorState());

        var removed = navigator.RemoveNode(child.Id);

        Assert.True(removed);
        Assert.Empty(parent.Children);
    }

    [Fact]
    public void Navigator_RemoveNode_RemovesRootFromRoots()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var root = navigator.CreateRoot(GeneratorNodeKind.Dto, new DtoGeneratorState());

        var removed = navigator.RemoveNode(root.Id);

        Assert.True(removed);
        Assert.Empty(session.Roots);
    }

    [Fact]
    public void Navigator_RemoveNode_ReturnsFalseForUnknownId()
    {
        var (session, navigator) = CreateSessionAndNavigator();

        var removed = navigator.RemoveNode(Guid.NewGuid());

        Assert.False(removed);
    }

    // ===== Artifact Index Tests =====

    [Fact]
    public void ArtifactIndex_ReturnsProjectDiscoveredArtifactsAfterSetProjectModel()
    {
        var (session, _) = CreateSessionAndNavigator();
        var projectModel = CreateProjectModel();

        session.Artifacts.SetProjectModel(projectModel);
        var dtos = session.Artifacts.GetDtos();

        Assert.Contains(dtos, d => d.Name == "UserDto" && d.IsFromProject && !d.IsFromSession);
    }

    [Fact]
    public void ArtifactIndex_ReturnsSessionCreatedArtifactsAfterNodeAdded()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var dtoState = new DtoGeneratorState { BaseName = "NewDto" };
        navigator.CreateRoot(GeneratorNodeKind.Dto, dtoState);

        var dtos = session.Artifacts.GetDtos();

        Assert.Contains(dtos, d => d.Name == "NewDto" && d.IsFromSession && !d.IsFromProject);
    }

    [Fact]
    public void QueryGeneratorDefinition_BuildPlan_RecomputesQueryServiceSuggestion()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var projectModel = CreateProjectModel();
        session.Artifacts.SetProjectModel(projectModel);

        var node = navigator.CreateRoot(GeneratorNodeKind.Query, new QueryGeneratorState
        {
            QueryName = "GetUsers",
            FeaturePath = "Users",
            CustomDtoName = "UserDto",
            CreateQueryServiceMethod = true,
            MethodName = "GetUsersAsync",
        });
        session.References.SetRef(node.Id, "Feature", new ArtifactRef(GeneratorNodeKind.Feature, ArtifactOrigin.Project, "Users", FeaturePath: "Users"));

        var definition = new QueryGeneratorDefinition();
        var serviceProvider = new QueryDefinitionServiceProvider(new CapturingAddQueryPlanService(), new QueryServiceSuggestionService());
        var targetRootPath = Path.GetTempPath();
        var plan = definition.BuildPlan(
            node,
            (QueryGeneratorState)node.State,
            session,
            new CoreWorkflowContext(
                new ProjectWorkspaceContext(targetRootPath, CqrsGenerator.Core.Configuration.GeneratorConfig.ForTargetRoot(targetRootPath), projectModel),
                null!,
                null!,
                serviceProvider));

        Assert.NotNull(plan);
        Assert.NotNull(serviceProvider.PlanService.LastFormState);
        Assert.NotNull(serviceProvider.PlanService.LastFormState!.QueryService);
        Assert.Equal(QueryServiceSuggestionMode.CreateNew, serviceProvider.PlanService.LastFormState.QueryService!.Mode);
    }

    [Fact]
    public void QueryGeneratorDefinition_Validate_Fails_WhenQueryServiceSuggestionIsBlocked()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var projectModel = new ProjectModel
        {
            Paths = new ProjectPaths("", "", "", "", "", "", "", "", ""),
            Features = [new FeatureInfo("Users", "Users", "")],
            Dtos = [new DtoInfo("UserDto", "Users", "", "", "")],
            QueryServices = [new QueryServiceInfo("IUsersQueryService", "Users", "iface.cs", null, null, QueryServiceImplementationPlacement.Missing, 0)],
            DependencyInjection = new DependencyInjectionInfo("", []),
        };
        session.Artifacts.SetProjectModel(projectModel);

        var node = navigator.CreateRoot(GeneratorNodeKind.Query, new QueryGeneratorState
        {
            QueryName = "GetUsers",
            FeaturePath = "Users",
            CustomDtoName = "UserDto",
            CreateQueryServiceMethod = true,
            MethodName = "GetUsersAsync",
        });
        session.References.SetRef(node.Id, "Feature", new ArtifactRef(GeneratorNodeKind.Feature, ArtifactOrigin.Project, "Users", FeaturePath: "Users"));

        var validation = new QueryGeneratorDefinition().Validate(node, (QueryGeneratorState)node.State, session);

        Assert.False(validation.IsValid);
        Assert.Contains("could not be resolved automatically", validation.Errors.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArtifactIndex_NoDuplicatesWhenProjectAndSessionHaveSameName()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        session.Artifacts.SetProjectModel(CreateProjectModel());
        var dtoState = new DtoGeneratorState { BaseName = "UserDto" };
        navigator.CreateRoot(GeneratorNodeKind.Dto, dtoState);

        var dtos = session.Artifacts.GetDtos();

        Assert.Single(dtos, d => d.Name == "UserDto");
    }

    [Fact]
    public void ArtifactIndex_FindByReference_WorksForProjectArtifacts()
    {
        var (session, _) = CreateSessionAndNavigator();
        session.Artifacts.SetProjectModel(CreateProjectModel());

        var found = session.Artifacts.Find(new ArtifactRef(
            GeneratorNodeKind.Dto,
            ArtifactOrigin.Project,
            "UserDto",
            FeaturePath: "Users"));

        Assert.NotNull(found);
        Assert.Equal("UserDto", found.Name);

        var notFound = session.Artifacts.Find(new ArtifactRef(
            GeneratorNodeKind.Dto,
            ArtifactOrigin.Project,
            "NonExistent"));
        Assert.Null(notFound);
    }

    [Fact]
    public void ArtifactIndex_ClearsAfterProjectReload()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        session.Artifacts.SetProjectModel(CreateProjectModel());
        var dtoState = new DtoGeneratorState { BaseName = "SessionDto" };
        navigator.CreateRoot(GeneratorNodeKind.Dto, dtoState);

        Assert.NotEmpty(session.Artifacts.GetDtos());

        session.Artifacts.ClearProjectModel();

        var dtosAfterClear = session.Artifacts.GetDtos();
        Assert.Contains(dtosAfterClear, d => d.Name == "SessionDto"); // session items survive
        Assert.DoesNotContain(dtosAfterClear, d => d.Name == "UserDto"); // project items cleared
    }

    // ===== Dangling Reference Detection Tests =====

    [Fact]
    public void FindUsages_FindsQueryReferencingDtoNode()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var dtoNode = navigator.CreateRoot(GeneratorNodeKind.Dto, new DtoGeneratorState { BaseName = "UserDto" });
        var queryState = new QueryGeneratorState
        {
            QueryName = "GetUsers",
        };
        var queryNode = navigator.CreateRoot(GeneratorNodeKind.Query, queryState);
        session.References.SetRef(queryNode.Id, "ResultDto", new ArtifactRef(GeneratorNodeKind.Dto, ArtifactOrigin.Session, "UserDto", dtoNode.Id));

        var usages = navigator.FindUsages(dtoNode.Id);

        Assert.Single(usages);
        Assert.Equal("ResultDto", usages[0].PropertyName);
    }

    [Fact]
    public void FindUsages_FindsRepositoryReferencingEntityNode()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var entityNode = navigator.CreateRoot(GeneratorNodeKind.Entity, new EntityGeneratorState { EntityName = "User" });
        var repoState = new RepositoryGeneratorState
        {
            InterfaceName = "IUserRepository",
            EntityRef = new ArtifactRef(GeneratorNodeKind.Entity, ArtifactOrigin.Session, "User", entityNode.Id),
        };
        navigator.CreateRoot(GeneratorNodeKind.Repository, repoState);

        var usages = navigator.FindUsages(entityNode.Id);

        Assert.Single(usages);
        Assert.Equal(nameof(RepositoryGeneratorState.EntityRef), usages[0].PropertyName);
    }

    [Fact]
    public void FindUsages_FindsCommandReferencingRepositoryNode()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var repoNode = navigator.CreateRoot(GeneratorNodeKind.Repository, new RepositoryGeneratorState { InterfaceName = "IUserRepository" });
        var cmdState = new CommandGeneratorState { CommandName = "CreateUser" };
        cmdState.RepositoryRefs.Add(new ArtifactRef(GeneratorNodeKind.Repository, ArtifactOrigin.Session, "IUserRepository", repoNode.Id));
        navigator.CreateRoot(GeneratorNodeKind.Command, cmdState);

        var usages = navigator.FindUsages(repoNode.Id);

        Assert.Single(usages);
        Assert.Equal(nameof(CommandGeneratorState.RepositoryRefs), usages[0].PropertyName);
    }

    [Fact]
    public void FindUsages_ReturnsEmptyForUnreferencedNode()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var unusedNode = navigator.CreateRoot(GeneratorNodeKind.Dto, new DtoGeneratorState { BaseName = "Orphan" });

        var usages = navigator.FindUsages(unusedNode.Id);

        Assert.Empty(usages);
    }

    [Fact]
    public void Navigator_RemoveNode_RejectsRemovalWhenNodeHasUsages()
    {
        var (session, navigator) = CreateSessionAndNavigator();
        var dtoNode = navigator.CreateRoot(GeneratorNodeKind.Dto, new DtoGeneratorState { BaseName = "UserDto" });
        var queryState = new QueryGeneratorState
        {
            QueryName = "GetUsers",
        };
        var queryNode = navigator.CreateRoot(GeneratorNodeKind.Query, queryState);
        session.References.SetRef(queryNode.Id, "ResultDto", new ArtifactRef(GeneratorNodeKind.Dto, ArtifactOrigin.Session, "UserDto", dtoNode.Id));

        var removed = navigator.RemoveNode(dtoNode.Id);

        Assert.False(removed);
    }

    [Fact]
    public void DefinitionValidation_DetectsDanglingResultDtoRef()
    {
        var catalog = new GeneratorDefinitionCatalog(CreateTestDefinitions());
        var (session, navigator) = CreateSessionAndNavigator();
        var queryState = new QueryGeneratorState
        {
            QueryName = "GetUsers",
        };
        var node = navigator.CreateRoot(GeneratorNodeKind.Query, queryState);
        session.References.SetRef(node.Id, "ResultDto", new ArtifactRef(GeneratorNodeKind.Dto, ArtifactOrigin.Session, "MissingDto", Guid.NewGuid()));
        var definition = catalog.GetDefinition(GeneratorNodeKind.Query);

        var result = definition.Validate(node, session);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("result"));
    }

    [Fact]
    public void DefinitionValidation_DetectsDanglingEntityRef()
    {
        var catalog = new GeneratorDefinitionCatalog(CreateTestDefinitions());
        var (session, navigator) = CreateSessionAndNavigator();
        var repoState = new RepositoryGeneratorState
        {
            InterfaceName = "IUserRepository",
            EntityRef = new ArtifactRef(GeneratorNodeKind.Entity, ArtifactOrigin.Session, "MissingEntity", Guid.NewGuid()),
        };
        var node = navigator.CreateRoot(GeneratorNodeKind.Repository, repoState);
        var definition = catalog.GetDefinition(GeneratorNodeKind.Repository);

        var result = definition.Validate(node, session);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("entity"));
    }

    [Fact]
    public void DefinitionValidation_DetectsDanglingRepositoryRefs()
    {
        var catalog = new GeneratorDefinitionCatalog(CreateTestDefinitions());
        var (session, navigator) = CreateSessionAndNavigator();
        var cmdState = new CommandGeneratorState { CommandName = "CreateUser" };
        cmdState.RepositoryRefs.Add(new ArtifactRef(GeneratorNodeKind.Repository, ArtifactOrigin.Session, "MissingRepository", Guid.NewGuid()));
        var node = navigator.CreateRoot(GeneratorNodeKind.Command, cmdState);
        var definition = catalog.GetDefinition(GeneratorNodeKind.Command);

        var result = definition.Validate(node, session);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("repository"));
    }

    [Fact]
    public void PlanBuilder_BuildsPlanInCorrectOrder()
    {
        var catalog = new GeneratorDefinitionCatalog(CreateTestDefinitions());
        var builder = new GenerationSessionPlanBuilder(catalog, new StubServiceProvider());
        var (session, navigator) = CreateSessionAndNavigator();

        var entity = navigator.CreateRoot(GeneratorNodeKind.Entity, new EntityGeneratorState { EntityName = "User" });
        entity.Status = GeneratorNodeStatus.Valid;
        var cmd = navigator.CreateRoot(GeneratorNodeKind.Command, new CommandGeneratorState { CommandName = "CreateUser" });
        cmd.Status = GeneratorNodeStatus.Valid;

        var plan = builder.BuildPlan(session, new CoreWorkflowContext(null!, null!, null!, new StubServiceProvider()));

        Assert.NotNull(plan);
    }

    [Fact]
    public void PlanBuilder_OnlyBuildsForRootNodes_NotCommittedChildren()
    {
        var catalog = new GeneratorDefinitionCatalog(CreateTestDefinitions());
        var builder = new GenerationSessionPlanBuilder(catalog, new StubServiceProvider());
        var (session, navigator) = CreateSessionAndNavigator();

        var root = navigator.CreateRoot(GeneratorNodeKind.Query, new QueryGeneratorState { QueryName = "GetUsers" });
        root.Status = GeneratorNodeStatus.Valid;
        var child = navigator.CreateChild(root, GeneratorNodeKind.Dto, new DtoGeneratorState { BaseName = "UserDto" });
        child.Status = GeneratorNodeStatus.Valid;
        child.Lifecycle = GeneratorNodeLifecycle.Committed;

        var plan = builder.BuildPlan(session, new CoreWorkflowContext(null!, null!, null!, new StubServiceProvider()));

        Assert.NotNull(plan);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void ValidationService_UpdatesNodeStatuses_FromDefinitionResults()
    {
        var catalog = new GeneratorDefinitionCatalog(CreateTestDefinitions());
        var validationService = new GenerationSessionValidationService(catalog);
        var (session, navigator) = CreateSessionAndNavigator();
        var validNode = navigator.CreateRoot(GeneratorNodeKind.Entity, new EntityGeneratorState { EntityName = "User" });
        var invalidNode = navigator.CreateRoot(
            GeneratorNodeKind.Query,
            new QueryGeneratorState
            {
                QueryName = "GetUsers",
            });
        session.References.SetRef(invalidNode.Id, "ResultDto", new ArtifactRef(GeneratorNodeKind.Dto, ArtifactOrigin.Session, "MissingDto", Guid.NewGuid()));

        var result = validationService.Validate(session);

        Assert.False(result.IsValid);
        Assert.Equal(GeneratorNodeStatus.Valid, validNode.Status);
        Assert.Equal(GeneratorNodeStatus.Invalid, invalidNode.Status);
    }

    private static IReadOnlyList<IGeneratorDefinition> CreateTestDefinitions()
    {
        return
        [
            new StubDefinition(GeneratorNodeKind.Feature, "Feature"),
            new StubDefinition(GeneratorNodeKind.Dto, "DTO"),
            new StubDefinition(GeneratorNodeKind.Query, "Query"),
            new StubDefinition(GeneratorNodeKind.Command, "Command"),
            new StubDefinition(GeneratorNodeKind.Repository, "Repository"),
            new StubDefinition(GeneratorNodeKind.Entity, "Entity"),
            new StubDefinition(GeneratorNodeKind.WebPage, "Web Page"),
        ];
    }

    private sealed class StubDefinition : GeneratorDefinition<object>
    {
        private readonly GeneratorNodeKind _kind;
        private readonly string _displayName;

        public StubDefinition(GeneratorNodeKind kind, string displayName)
        {
            _kind = kind;
            _displayName = displayName;
        }

        public override GeneratorNodeKind Kind => _kind;
        public override string DisplayName => _displayName;

        public override object CreateInitialState(GeneratorCreationContext context) => new();

        public override IGeneratorNodeEditorViewModel CreateEditor(
            GeneratorNode node, object state, GenerationSession session, GeneratorSessionServices services)
            => throw new NotImplementedException();

        public override GeneratorValidationResult Validate(GeneratorNode node, object state, GenerationSession session)
        {
            var errors = new List<string>();

            if (state is CommandGeneratorState cmdState)
            {
                var dangling = cmdState.RepositoryRefs
                    .Where(reference => reference.NodeId.HasValue)
                    .Select(reference => session.FindNode(reference.NodeId!.Value))
                    .Where(n => n is null)
                    .Count();
                if (dangling > 0)
                    errors.Add($"{dangling} dangling repository reference(s)");
            }
            else if (state is RepositoryGeneratorState repoState)
            {
                if (repoState.EntityRef?.NodeId.HasValue == true &&
                    session.FindNode(repoState.EntityRef.NodeId.Value) is null)
                    errors.Add("Dangling entity reference");
            }
            else if (state is QueryGeneratorState)
            {
                var resultDtoRef = session.References.GetRef(node.Id, "ResultDto");
                if (resultDtoRef?.NodeId.HasValue == true &&
                    session.FindNode(resultDtoRef.NodeId.Value) is null)
                    errors.Add("Dangling result DTO reference");
            }

            return errors.Count > 0
                ? GeneratorValidationResult.Error(string.Join("; ", errors))
                : GeneratorValidationResult.Valid;
        }

        public override GeneratorPreview BuildPreview(GeneratorNode node, object state, GenerationSession session)
            => new(node, _displayName, [], [], [], []);

        public override GenerationPlan BuildPlan(GeneratorNode node, object state, GenerationSession session, CoreWorkflowContext core)
            => new();
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class QueryDefinitionServiceProvider : IServiceProvider
    {
        public QueryDefinitionServiceProvider(CapturingAddQueryPlanService planService, IQueryServiceSuggestionService suggestionService)
        {
            PlanService = planService;
            SuggestionService = suggestionService;
        }

        public CapturingAddQueryPlanService PlanService { get; }

        public IQueryServiceSuggestionService SuggestionService { get; }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAddQueryPlanService))
            {
                return PlanService;
            }

            if (serviceType == typeof(IQueryServiceSuggestionService))
            {
                return SuggestionService;
            }

            return null;
        }
    }

    private sealed class CapturingAddQueryPlanService : IAddQueryPlanService
    {
        public AddQueryFormState? LastFormState { get; private set; }

        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddQueryFormState formState)
        {
            LastFormState = formState;
            return new GenerationPlan();
        }
    }
}
