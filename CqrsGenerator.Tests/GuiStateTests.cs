using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Validation.Rules;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Services.Generators;
using CqrsGenerator.Gui.Services.Updates;
using CqrsGenerator.Gui.Session;
using CqrsGenerator.Gui.Session.Definitions;
using CqrsGenerator.Gui.Session.States;
using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;
using CqrsGenerator.Gui.Views;

namespace CqrsGenerator.Tests;

public partial class GuiStateTests
{
    [Fact]
    public void AddQuery_RemoveParameterCommand_RemovesSpecificEntry()
    {
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        session.AddParameterCommand.Execute(null);
        session.AddParameterCommand.Execute(null);

        var first = session.Parameters[0];
        var second = session.Parameters[1];

        session.RemoveParameterCommand.Execute(first);

        Assert.Single(session.Parameters);
        Assert.Same(second, session.Parameters[0]);
    }

    [Fact]
    public void CreateDto_RemovePropertyCommand_RecomputesCompletion()
    {
        var session = new DtoRootSessionViewModel(
            actionDescriptor: null,
            new StubAddDtoPlanService(),
            isStandalone: false);
        session.DtoNameCyclic.Text = "UserLookup";
        session.PropertyEditor.AddEntryCommand.Execute(null);

        Assert.Equal(2, session.PropertyEditor.Entries.Count);

        var incomplete = session.PropertyEditor.Entries[1];
        incomplete.Name = string.Empty;

        Assert.False(session.CanComplete);

        session.PropertyEditor.RemoveEntryCommand.Execute(incomplete);

        Assert.Single(session.PropertyEditor.Entries);
        Assert.True(session.CanComplete);
    }

    [Fact]
    public void RootSessionChanged_FiresOnlyWhenRootIdentityChanges()
    {
        var workspaceStore = CreateWorkspaceStore(Path.GetTempPath());
        var (session_s2, navigator_s2) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, session_s2, navigator_s2, CreateEmptyDefinitionCatalog(), new StubServiceProvider());
        var rootSession = new TestMutableRootSession();
        var rootSessionChangedCount = 0;

        stack.RootSessionChanged += _ => rootSessionChangedCount++;

        stack.OpenRoot(rootSession);
        rootSession.DisplayName = "Updated Root";
        workspaceStore.SetState(workspaceStore.State with
        {
            StatusText = "Project rescanned."
        });
        stack.CloseRootCommand.Execute(null);

        Assert.Equal(2, rootSessionChangedCount);
    }

    [Fact]
    public void WorkspaceCapabilityUpdate_DoesNotReloadWorkspaceAwareRootSession()
    {
        var workspaceStore = CreateWorkspaceStore(Path.GetTempPath());
        var (session_s3, navigator_s3) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, session_s3, navigator_s3, CreateEmptyDefinitionCatalog(), new StubServiceProvider());
        var rootSession = new TrackingWorkspaceAwareRootSession();

        stack.OpenRoot(rootSession);
        Assert.Equal(1, rootSession.UpdateWorkspaceCallCount);

        workspaceStore.SetState(workspaceStore.State with
        {
            CanBuildPlan = true,
            StatusText = "Capabilities refreshed."
        });

        Assert.Equal(1, rootSession.UpdateWorkspaceCallCount);
    }

    [Fact]
    public void OpenRoot_NodeEditorRoot_ActivatesRootNode()
    {
        var workspaceStore = CreateWorkspaceStore(Path.GetTempPath());
        var (session, navigator) = CreateSessionAndNavigator();
        var definitions = new GeneratorDefinitionCatalog([new StubNodeEditorDefinition()]);
        var stack = new GeneratorStackViewModel(workspaceStore, session, navigator, definitions, new StubServiceProvider());
        var rootSession = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        stack.OpenRoot(rootSession);

        Assert.NotNull(rootSession.Node);
        Assert.Equal(rootSession.Node, session.ActiveNode);
        Assert.Same(rootSession, stack.ActiveSession);
        Assert.True(stack.HasActiveNode);
    }

    [Fact]
    public void CloseRoot_ClearsSessionGraph_AndActiveSession()
    {
        var workspaceStore = CreateWorkspaceStore(Path.GetTempPath());
        var (session, navigator) = CreateSessionAndNavigator();
        var definitions = new GeneratorDefinitionCatalog([new StubNodeEditorDefinition()]);
        var stack = new GeneratorStackViewModel(workspaceStore, session, navigator, definitions, new StubServiceProvider());
        var rootSession = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        stack.OpenRoot(rootSession);
        stack.CloseRootCommand.Execute(null);

        Assert.Empty(session.Roots);
        Assert.Null(session.ActiveNode);
        Assert.Null(stack.ActiveSession);
        Assert.Null(stack.RootSession);
        Assert.False(stack.HasActiveNode);
    }

    [Fact]
    public async Task BuildPlanSuccess_UpdatesWorkspaceStateAndPreviewSnapshot()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");

        var (generationSession, navigator) = CreateSessionAndNavigator();
        var definitions = new GeneratorDefinitionCatalog([new TestPlanDefinition()]);
        var coordinator = CreateCoordinator(generationSession, definitions);
        var sessionService = CreateSessionService(workspaceStore, coordinator);
        var session = CreateGraphBackedRootSession(navigator, () => plan);

        await sessionService.BuildPlanAsync(session, CancellationToken.None);

        Assert.Equal(PlanBuildStatus.Built, workspaceStore.State.PlanBuildStatus);
        Assert.NotNull(workspaceStore.State.CurrentPlan);
        Assert.NotNull(workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.NotEmpty(workspaceStore.State.CurrentPlanPreviewSnapshot.RootNodes);
        Assert.Equal(
            workspaceStore.State.CurrentPreparedApplyPackage!.Fingerprint,
            workspaceStore.State.CurrentPlanPreviewSnapshot.PackageFingerprint);
        Assert.Null(workspaceStore.State.PlanBuildErrorMessage);
        Assert.Null(workspaceStore.State.LastErrorDetails);
    }

    [Fact]
    public async Task BuildPlanSuccess_PreservesGenerationWarningsInWorkspaceAndSnapshot()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        plan.AddWarning("Manual validation is required.");

        var (generationSession, navigator) = CreateSessionAndNavigator();
        var definitions = new GeneratorDefinitionCatalog([new TestPlanDefinition()]);
        var sessionService = CreateSessionService(workspaceStore, CreateCoordinator(generationSession, definitions));
        var session = CreateGraphBackedRootSession(navigator, () => plan);

        await sessionService.BuildPlanAsync(session, CancellationToken.None);

        Assert.Single(workspaceStore.State.GenerationWarnings);
        Assert.Equal("Manual validation is required.", workspaceStore.State.GenerationWarnings[0].Message);
        Assert.Single(workspaceStore.State.CurrentPlanPreviewSnapshot.GenerationWarnings);
        Assert.Equal(
            workspaceStore.State.GenerationWarnings[0].Message,
            workspaceStore.State.CurrentPlanPreviewSnapshot.GenerationWarnings[0].Message);
    }

    [Fact]
    public async Task BuildPlanSuccess_QueryGraphRecomputesQueryServiceSuggestion_WithoutWarning()
    {
        using var tmp = new TempProject();
        var projectModel = CreateProjectModel(tmp.Root);
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        workspaceStore.SetState(workspaceStore.State with
        {
            ProjectModel = projectModel,
        });

        var (generationSession, navigator) = CreateSessionAndNavigator();
        generationSession.Artifacts.SetProjectModel(projectModel);
        var queryNode = navigator.CreateRoot(GeneratorNodeKind.Query, new QueryGeneratorState
        {
            QueryName = "GetUsers",
            FeaturePath = "Users",
            CustomDtoName = "UserDto",
            CreateQueryServiceMethod = true,
            MethodName = "GetUsersAsync",
        });
        var rootSession = new TestPlanBuildingRootSession { Node = queryNode };
        var planService = new CapturingAddQueryPlanService();
        var definitions = new GeneratorDefinitionCatalog([new QueryGeneratorDefinition()]);
        var coordinator = CreateCoordinator(
            generationSession,
            definitions,
            new QueryBuildServiceProvider(planService));
        var sessionService = CreateSessionService(workspaceStore, coordinator);

        await sessionService.BuildPlanAsync(rootSession, CancellationToken.None);

        Assert.Equal(PlanBuildStatus.Built, workspaceStore.State.PlanBuildStatus);
        Assert.NotNull(planService.LastFormState);
        Assert.NotNull(planService.LastFormState!.QueryService);
        Assert.DoesNotContain(
            workspaceStore.State.GenerationWarnings,
            warning => warning.Message.Contains("Query service is not available.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildPlanFailure_WhenSessionGraphIsInvalid_ClearsPlanAndPublishesErrorState()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var existingPlan = new GenerationPlan();
        existingPlan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "ExistingQuery.cs"), "public class ExistingQuery {}");

        var previewService = new PlanPreviewService(new DiffService());
        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = existingPlan,
            CurrentPreparedApplyPackage = PreparePackage(existingPlan, workspaceStore.State.TargetRootPath!),
            CurrentPlanPreviewSnapshot = previewService.Build(PreparePackage(existingPlan, workspaceStore.State.TargetRootPath!))
        });

        var (generationSession, navigator) = CreateSessionAndNavigator();
        var definitions = new GeneratorDefinitionCatalog([new TestPlanDefinition()]);
        var coordinator = CreateCoordinator(generationSession, definitions);
        var sessionService = CreateSessionService(workspaceStore, coordinator);
        var session = CreateGraphBackedRootSession(navigator, () => new GenerationPlan(), validationError: "Exploded.");

        await sessionService.BuildPlanAsync(session, CancellationToken.None);

        Assert.Null(workspaceStore.State.CurrentPlan);
        Assert.Null(workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.Empty(workspaceStore.State.CurrentPlanPreviewSnapshot.RootNodes);
        Assert.Equal(PlanBuildStatus.Failed, workspaceStore.State.PlanBuildStatus);
        Assert.Equal("Query: Exploded.", workspaceStore.State.PlanBuildErrorMessage);
        Assert.Contains("Exploded.", workspaceStore.State.LastErrorDetails);
    }

    [Fact]
    public void PreviewViewModel_UsesIncomingSnapshotAndDropsStaleNodesAfterFailure()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var previewService = new PlanPreviewService(new DiffService());
        var preview = new GenerationPlanPreviewViewModel(workspaceStore);

        var successfulPlan = new GenerationPlan();
        successfulPlan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");

        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = successfulPlan,
            CurrentPreparedApplyPackage = PreparePackage(successfulPlan, workspaceStore.State.TargetRootPath!),
            CurrentPlanPreviewSnapshot = previewService.Build(PreparePackage(successfulPlan, workspaceStore.State.TargetRootPath!))
        });

        Assert.NotEmpty(preview.RootNodes);

        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = null,
            CurrentPlanPreviewSnapshot = PlanPreviewSnapshot.Error("Plan build failed.")
        });

        Assert.Empty(preview.RootNodes);
        Assert.True(preview.ShowEmptyState);
        Assert.Equal("Plan build failed.", preview.EmptyStateText);
        Assert.True(preview.SelectedPreview.IsEmpty);
    }

    [Fact]
    public void MainWindow_BuildAvailabilityTracksActiveGraphRoot()
    {
        var warningService = CreateArchitectureWarningService();
        var workspaceStore = CreateWorkspaceStore(Path.GetTempPath());
        var coordinator = CreateCoordinator();
        var sessionService = CreateSessionService(workspaceStore, coordinator, warningService);
        var generatorCatalog = CreateGeneratorCatalog();
        var (session1, navigator1) = CreateSessionAndNavigator();
        var generatorHost = new GeneratorHostViewModel(workspaceStore, generatorCatalog, sessionService, session1, navigator1, CreateEmptyDefinitionCatalog(), new StubServiceProvider());
        var mainWindow = new MainWindowViewModel(
            new StubThemeService(),
            workspaceStore,
            sessionService,
            generatorHost,
            CreateAppUpdateViewModel(workspaceStore));

        var action = mainWindow.GeneratorHost.ActionLauncher.Groups
            .SelectMany(group => group.Actions)
            .Single(item => item.Action.ActionId == "add-query");

        action.OpenCommand.Execute(null);

        Assert.True(mainWindow.BuildPlanCommand.CanExecute(null));
        Assert.True(mainWindow.MessagesPanel.CanBuildPlan);

        var rootSession = Assert.IsType<AddQueryRootSessionViewModel>(mainWindow.GeneratorHost.Stack.RootSession);

        rootSession.QueryName = string.Empty;

        Assert.False(rootSession.CanBuildPlan);
        Assert.True(mainWindow.BuildPlanCommand.CanExecute(null));
        Assert.True(mainWindow.MessagesPanel.CanBuildPlan);
    }

    [Fact]
    public async Task OpenProject_WithActiveRootSession_PreservesRootSessionWithoutRecursion()
    {
        using var tmp = new TempProject();
        var workspaceStore = new WorkspaceStore();
        var warningService = CreateArchitectureWarningService();
        var shellService = new WorkspaceShellService(
            new FixedProjectOpenService(tmp.Root),
            new StubProjectScanService());
        var sessionService = new WorkspaceSessionService(
            workspaceStore,
            shellService,
            CreateCoordinator(),
            new StubWorkspaceApplyService(),
            warningService);
        var generatorCatalog = CreateGeneratorCatalog();
        var (session2, navigator2) = CreateSessionAndNavigator();
        var generatorHost = new GeneratorHostViewModel(workspaceStore, generatorCatalog, sessionService, session2, navigator2, CreateEmptyDefinitionCatalog(), new StubServiceProvider());
        var mainWindow = new MainWindowViewModel(
            new StubThemeService(),
            workspaceStore,
            sessionService,
            generatorHost,
            CreateAppUpdateViewModel(workspaceStore));

        var action = mainWindow.GeneratorHost.ActionLauncher.Groups
            .SelectMany(group => group.Actions)
            .Single(item => item.Action.ActionId == "add-query");

        action.OpenCommand.Execute(null);
        var rootBeforeOpen = Assert.IsType<AddQueryRootSessionViewModel>(mainWindow.GeneratorHost.Stack.RootSession);

        await mainWindow.OpenProjectCommand.ExecuteAsync(null);

        var rootAfterOpen = Assert.IsType<AddQueryRootSessionViewModel>(mainWindow.GeneratorHost.Stack.RootSession);
        Assert.Same(rootBeforeOpen, rootAfterOpen);
        Assert.True(workspaceStore.State.IsProjectLoaded);
        Assert.Equal("Project discovery completed.", workspaceStore.State.StatusText);
    }

    [Fact]
    public void PlanExplorerSelection_ShowsFolderAndCreatedFilePreview()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var previewService = new PlanPreviewService(new DiffService());
        var preview = new GenerationPlanPreviewViewModel(workspaceStore);
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");

        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPreparedApplyPackage = PreparePackage(plan, workspaceStore.State.TargetRootPath!),
            CurrentPlanPreviewSnapshot = previewService.Build(PreparePackage(plan, workspaceStore.State.TargetRootPath!))
        });

        var folderNode = Assert.Single(preview.RootNodes);
        preview.SelectedPlanNode = folderNode;
        Assert.True(preview.SelectedPreview.IsDirectory);

        var fileNode = folderNode.Children[0].Children[0].Children[0].Children[0];
        preview.SelectedPlanNode = fileNode;

        Assert.True(preview.SelectedPreview.IsCreatedFile);
        Assert.Contains("GetUsersQuery", preview.SelectedPreview.Content);
    }









    [Fact]
    public void AddQuery_DiscoveredDto_CyclePrefixUpdatesPrimaryText()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        session.UpdateWorkspace(projectContext);
        session.QueryName = "GetUsers";

        var selected = Assert.IsType<WrappedListItem>(session.ResultTypePicker.SelectedItem);
        Assert.Equal("UserDto", selected.PrimaryText);

        session.ResultTypePicker.CyclePrefixForwardCommand.Execute(null);

        selected = Assert.IsType<WrappedListItem>(session.ResultTypePicker.SelectedItem);
        Assert.Equal("List<UserDto>", selected.PrimaryText);

        session.ResultTypePicker.CyclePrefixForwardCommand.Execute(null);

        selected = Assert.IsType<WrappedListItem>(session.ResultTypePicker.SelectedItem);
        Assert.Equal("IEnumerable<UserDto>", selected.PrimaryText);
    }

    [Fact]
    public void AddQuery_QueryNameAutoDerivesMethodName_AndRespectsUserOverride()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());
        session.UpdateWorkspace(projectContext);
        session.QueryName = "GetUsers";
        Assert.Equal("GetUsers", session.MethodNameCyclic.Text);
        Assert.Equal("GetUsersAsync", session.MethodNameCyclic.FullText);

        session.MethodNameCyclic.Text = "LoadUsers";
        session.QueryName = "GetCustomers";

        Assert.Equal("LoadUsers", session.MethodNameCyclic.Text);
        Assert.Equal("LoadUsersAsync", session.MethodNameCyclic.FullText);
    }

    [Fact]
    public void AddQuery_ResultSearchClear_DoesNotResetDtoListOrQueryService()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var sessionService = CreateSessionService(workspaceStore);
        var (session3, navigator3) = CreateSessionAndNavigator();
        var generatorHost = new GeneratorHostViewModel(workspaceStore, CreateGeneratorCatalog(), sessionService, session3, navigator3, CreateEmptyDefinitionCatalog(), new StubServiceProvider());
        var mainWindow = new MainWindowViewModel(
            new StubThemeService(),
            workspaceStore,
            sessionService,
            generatorHost,
            CreateAppUpdateViewModel(workspaceStore));

        var action = mainWindow.GeneratorHost.ActionLauncher.Groups
            .SelectMany(group => group.Actions)
            .Single(item => item.Action.ActionId == "add-query");

        action.OpenCommand.Execute(null);

        var rootSession = Assert.IsType<AddQueryRootSessionViewModel>(mainWindow.GeneratorHost.Stack.RootSession);
        rootSession.UpdateWorkspace(projectContext);
        rootSession.QueryName = "GetUsers";

        var initialDtos = rootSession.ResultTypeItems.Select(dto => dto.Name).ToArray();
        Assert.NotEmpty(initialDtos);
        Assert.Equal("IUsersQueryService", rootSession.QueryServiceStatus.DisplayText);

        rootSession.ResultTypePicker.SearchText = "UserLookup";
        rootSession.ResultTypePicker.SearchText = string.Empty;

        Assert.Equal(initialDtos, rootSession.ResultTypeItems.Select(dto => dto.Name).ToArray());
        Assert.Equal("IUsersQueryService", rootSession.QueryServiceStatus.DisplayText);
        Assert.NotNull(rootSession.SelectedFeature);
    }



    [Fact]
    public async Task ApplySuccess_TriggersRescanAndWarningRefresh()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var trackingWarningService = new TrackingArchitectureWarningService();
        var scanService = new TrackingProjectScanService();
        var shellService = new WorkspaceShellService(
            new StubProjectOpenService(),
            scanService);
        var applyService = new WorkspaceApplyService(
            new StubDialogService(confirmResult: true),
            new StrictPlanApplier());
        var sessionService = new WorkspaceSessionService(
            workspaceStore,
            shellService,
            CreateCoordinator(),
            applyService,
            trackingWarningService);

        var generatedFile = Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs");
        var plan = new GenerationPlan();
        plan.AddCreateFile(generatedFile, "public class GetUsersQuery {}");
        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = plan,
            CurrentPreparedApplyPackage = PreparePackage(plan, tmp.Root),
            CurrentPlanPreviewSnapshot = new PlanPreviewService(new DiffService()).Build(PreparePackage(plan, tmp.Root))
        });

        await sessionService.ApplyPlanAsync(CancellationToken.None);

        Assert.True(File.Exists(generatedFile));
        Assert.Equal(1, scanService.ScanCount);
        Assert.True(trackingWarningService.RefreshCount >= 1);
        Assert.Equal("Plan applied and project rescanned.", workspaceStore.State.ApplyResultMessage);
    }

    [Fact]
    public void MessagesPanel_SeparatesGenerationWarningsFromArchitectureWarnings()
    {
        var workspaceStore = new WorkspaceStore();
        workspaceStore.SetState(WorkspaceState.Empty with
        {
            StatusText = "Plan built.",
            GenerationWarnings = [new GenerationWarning("Generated file needs manual review.")],
            Warnings =
            [
                new ArchitectureWarning(
                    "CQRS-001",
                    "Warning",
                    "Single query service",
                    "Multiple query services were detected.",
                    "Users"),
            ],
        });

        var panel = new MessagesPanelViewModel(workspaceStore);

        Assert.Single(panel.GenerationWarnings);
        Assert.Equal("Generated file needs manual review.", panel.GenerationWarnings[0].Message);
        Assert.Single(panel.ArchitectureWarnings);
        Assert.Equal("CQRS-001", panel.ArchitectureWarnings[0].Code);
        Assert.True(panel.HasGenerationWarnings);
        Assert.True(panel.HasArchitectureWarnings);
    }

    [Fact]
    public async Task WorkspaceApplyService_ConfirmationReadsPreparedPackageWarnings()
    {
        using var tmp = new TempProject();
        var dialogService = new CapturingDialogService(confirmResult: false);
        var applyService = new WorkspaceApplyService(dialogService, new StrictPlanApplier());
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        plan.AddWarning("Review this change before apply.");
        var package = PreparePackage(plan, tmp.Root);

        var result = await applyService.ApplyPlanAsync(package, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.Cancelled);
        Assert.NotNull(dialogService.LastPackage);
        Assert.Single(dialogService.LastPackage!.Warnings);
        Assert.Equal("Review this change before apply.", dialogService.LastPackage.Warnings[0].Message);
    }

    [Fact]
    public async Task ApplyPlan_WithoutCurrentPlan_UsesPreparedPackageContract()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var applyService = new TrackingWorkspaceApplyService(WorkspaceApplyExecutionResult.Success("Plan applied."));
        var shellService = new StubWorkspaceShellService(
            loadProjectResult: ProjectLoadResult.Success(
                "Project discovery completed.",
                GeneratorConfig.ForTargetRoot(tmp.Root),
                CreateProjectModel(tmp.Root)));
        var sessionService = new WorkspaceSessionService(
            workspaceStore,
            shellService,
            CreateCoordinator(),
            applyService,
            CreateArchitectureWarningService());

        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        var package = PreparePackage(plan, tmp.Root);
        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = null,
            CurrentPreparedApplyPackage = package,
            CurrentPlanPreviewSnapshot = new PlanPreviewService(new DiffService()).Build(package)
        });

        await sessionService.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(1, applyService.ApplyCallCount);
        Assert.Equal(1, shellService.LoadProjectCallCount);
        Assert.Equal("Plan applied and project rescanned.", workspaceStore.State.ApplyResultMessage);
    }

    [Fact]
    public async Task ApplyCancelled_PreservesPreparedPackageAndApplyCapability()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var applyService = new TrackingWorkspaceApplyService(WorkspaceApplyExecutionResult.CancelledByUser("Plan apply cancelled."));
        var sessionService = new WorkspaceSessionService(
            workspaceStore,
            new StubWorkspaceShellService(),
            CreateCoordinator(),
            applyService,
            CreateArchitectureWarningService());

        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        var package = PreparePackage(plan, tmp.Root);
        var snapshot = new PlanPreviewService(new DiffService()).Build(package);
        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = plan,
            CurrentPreparedApplyPackage = package,
            CurrentPlanPreviewSnapshot = snapshot
        });
        sessionService.RefreshRootSessionState(null);

        await sessionService.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(1, applyService.ApplyCallCount);
        Assert.Same(package, workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.Equal(snapshot.PackageFingerprint, workspaceStore.State.CurrentPlanPreviewSnapshot.PackageFingerprint);
        Assert.Equal("Plan apply cancelled.", workspaceStore.State.ApplyResultMessage);
        Assert.True(workspaceStore.State.CanApplyPlan);
    }

    [Fact]
    public async Task ApplyFailure_ClearsPreparedPackageAndDisablesApply()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var applyService = new TrackingWorkspaceApplyService(WorkspaceApplyExecutionResult.Failure("Plan apply failed.", "Disk full."));
        var sessionService = new WorkspaceSessionService(
            workspaceStore,
            new StubWorkspaceShellService(),
            CreateCoordinator(),
            applyService,
            CreateArchitectureWarningService());

        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        var package = PreparePackage(plan, tmp.Root);
        var snapshot = new PlanPreviewService(new DiffService()).Build(package);
        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = plan,
            CurrentPreparedApplyPackage = package,
            CurrentPlanPreviewSnapshot = snapshot
        });
        sessionService.RefreshRootSessionState(null);

        await sessionService.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(1, applyService.ApplyCallCount);
        Assert.Null(workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.Equal(snapshot.PackageFingerprint, workspaceStore.State.CurrentPlanPreviewSnapshot.PackageFingerprint);
        Assert.Equal("Plan apply failed.", workspaceStore.State.ApplyResultMessage);
        Assert.Equal("Disk full.", workspaceStore.State.PlanBuildErrorMessage);
        Assert.False(workspaceStore.State.CanApplyPlan);
    }

    [Fact]
    public async Task ApplySuccess_WithRescanFailure_InvalidatesPreparedState()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var applyService = new TrackingWorkspaceApplyService(WorkspaceApplyExecutionResult.Success("Plan applied."));
        var shellService = new StubWorkspaceShellService(
            loadProjectResult: ProjectLoadResult.Failure("Project rescan failed.", "Scan exploded."));
        var sessionService = new WorkspaceSessionService(
            workspaceStore,
            shellService,
            CreateCoordinator(),
            applyService,
            CreateArchitectureWarningService());

        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        var package = PreparePackage(plan, tmp.Root);
        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = plan,
            CurrentPreparedApplyPackage = package,
            CurrentPlanPreviewSnapshot = new PlanPreviewService(new DiffService()).Build(package)
        });

        await sessionService.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(1, applyService.ApplyCallCount);
        Assert.Null(workspaceStore.State.CurrentPlan);
        Assert.Null(workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.True(workspaceStore.State.CurrentPlanPreviewSnapshot.IsError);
        Assert.Equal("Plan applied, but project rescan failed.", workspaceStore.State.StatusText);
        Assert.Equal("Scan exploded.", workspaceStore.State.PlanBuildErrorMessage);
        Assert.False(workspaceStore.State.CanApplyPlan);
    }

    [Fact]
    public async Task ApplyPlan_WithFingerprintMismatch_IsBlockedAndClearsPreparedPackage()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var sessionService = CreateSessionService(workspaceStore);
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        var package = PreparePackage(plan, tmp.Root);
        var builtSnapshot = new PlanPreviewService(new DiffService()).Build(package);
        var snapshot = new PlanPreviewSnapshot
        {
            SummaryItems = builtSnapshot.SummaryItems,
            RootNodes = builtSnapshot.RootNodes,
            DefaultPreview = builtSnapshot.DefaultPreview,
            EmptyStateText = builtSnapshot.EmptyStateText,
            GenerationWarnings = builtSnapshot.GenerationWarnings,
            IsError = builtSnapshot.IsError,
            PackageFingerprint = "stale",
        };

        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = plan,
            CurrentPreparedApplyPackage = package,
            CurrentPlanPreviewSnapshot = snapshot,
            CanApplyPlan = true
        });

        await sessionService.ApplyPlanAsync(CancellationToken.None);

        Assert.Null(workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.Equal("Apply blocked: the prepared plan is stale.", workspaceStore.State.ApplyResultMessage);
        Assert.False(workspaceStore.State.CanApplyPlan);
    }

    [Fact]
    public async Task RescanProjectFailure_InvalidatesPreparedStateAndDisablesApply()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var shellService = new StubWorkspaceShellService(
            loadProjectResult: ProjectLoadResult.Failure("Project rescan failed.", "Project scan failed."));
        var sessionService = new WorkspaceSessionService(
            workspaceStore,
            shellService,
            CreateCoordinator(),
            new StubWorkspaceApplyService(),
            CreateArchitectureWarningService());

        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");
        var package = PreparePackage(plan, tmp.Root);
        workspaceStore.SetState(workspaceStore.State with
        {
            CurrentPlan = plan,
            CurrentPreparedApplyPackage = package,
            CurrentPlanPreviewSnapshot = new PlanPreviewService(new DiffService()).Build(package)
        });
        sessionService.RefreshRootSessionState(null);

        await sessionService.RescanProjectAsync(CancellationToken.None);

        Assert.Equal(1, shellService.LoadProjectCallCount);
        Assert.Null(workspaceStore.State.CurrentPlan);
        Assert.Null(workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.True(workspaceStore.State.CurrentPlanPreviewSnapshot.IsError);
        Assert.Equal("Project rescan failed.", workspaceStore.State.StatusText);
        Assert.Equal("Project scan failed.", workspaceStore.State.PlanBuildErrorMessage);
        Assert.False(workspaceStore.State.CanApplyPlan);
    }

    [Fact]
    public void ArchitectureWarnings_AreRefreshedByCentralService_NotByAddQuerySession()
    {
        using var tmp = new TempProject();
        var rootPath = tmp.Root;
        var projectModel = new ProjectModel
        {
            Paths = CreateProjectModel(rootPath).Paths,
            Features =
            [
                new FeatureInfo("Users", "Users", Path.Combine(rootPath, "Application", "Features", "Users")),
            ],
            Dtos = [],
            QueryServices =
            [
                new QueryServiceInfo("IUsersQueryService", "Users", Path.Combine(rootPath, "Application", "Features", "Users", "Interfaces", "IUsersQueryService.cs"), "UsersQueryService", Path.Combine(rootPath, "Infrastructure", "Data", "QueryServices", "UsersQueryService.cs")),
                new QueryServiceInfo("IUsersQueryServiceAlt", "Users", Path.Combine(rootPath, "Application", "Features", "Users", "Interfaces", "IUsersQueryServiceAlt.cs"), "UsersQueryServiceAlt", Path.Combine(rootPath, "Infrastructure", "Data", "QueryServices", "UsersQueryServiceAlt.cs")),
            ],
            DependencyInjection = new DependencyInjectionInfo(Path.Combine(rootPath, "Infrastructure", "DependencyInjection.cs"), []),
        };
        var projectContext = new ProjectWorkspaceContext(rootPath, GeneratorConfig.ForTargetRoot(rootPath), projectModel);

        _ = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        var warnings = CreateArchitectureWarningService().GetWarnings(projectContext);

        Assert.Single(warnings);
        Assert.Equal(new SingleQueryServicePerFeatureRule().RuleCode, warnings[0].Code);
    }

    [Fact]
    public void ViewLocator_MatchesOnlyGeneratorSessions()
    {
        var viewLocator = new ViewLocator();

        Assert.False(viewLocator.Match("not a session"));
        Assert.False(viewLocator.Match(new object()));
        Assert.True(viewLocator.Match(new TestRootSession()));
    }

    [Fact]
    public void ViewLocator_ResolvesGeneratorViewTypeAndFallsBackForUnknownSession()
    {
        var viewLocator = new ViewLocator();
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        var resolvedViewType = ViewLocator.ResolveViewType(session);
        var fallbackView = viewLocator.Build(new UnknownGeneratorSession());

        Assert.Equal(typeof(AddQueryRootSessionView), resolvedViewType);
        var fallbackText = Assert.IsType<TextBlock>(fallbackView);
        Assert.Contains(nameof(UnknownGeneratorSession), fallbackText.Text);
    }

    private static ProjectModel CreateProjectModel(string rootPath)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var applicationPath = Path.Combine(fullRootPath, "Application");
        var infrastructurePath = Path.Combine(fullRootPath, "Infrastructure");
        var webPath = Path.Combine(fullRootPath, "Web");
        var featurePath = Path.Combine(applicationPath, "Features", "Users");

        return new ProjectModel
        {
            Paths = new ProjectPaths(
                fullRootPath,
                applicationPath,
                Path.Combine(applicationPath, "Features"),
                infrastructurePath,
                Path.Combine(infrastructurePath, "Data", "QueryServices"),
                Path.Combine(infrastructurePath, "Data", "Repositories"),
                Path.Combine(infrastructurePath, "DependencyInjection.cs"),
                webPath,
                Path.Combine(webPath, "Features")),
            Features =
            [
                new FeatureInfo("Users", "Users", featurePath),
            ],
            Dtos =
            [
                new DtoInfo("UserDto", "Users", Path.Combine(featurePath, "DTOs", "UserDto.cs"), "Application.Features.Users.DTOs", "UserDto"),
            ],
            DtoSubfolders =
            [
                new DtoSubfolderInfo("Users", "Admin"),
            ],
            Repositories =
            [
                new RepositoryInfo("IUserRepository", Path.Combine(fullRootPath, "Application", "Common", "Interfaces", "Repositories", "IUserRepository.cs")),
            ],
            Entities =
            [
                new EntityInfo("User", Path.Combine(fullRootPath, "Domain", "Entities", "User.cs"), "User", "Domain.Entities", null),
                new EntityInfo("AdminUser", Path.Combine(fullRootPath, "Domain", "Entities", "Admin", "AdminUser.cs"), "Admin/AdminUser", "Domain.Entities.Admin", "Admin"),
            ],
            QueryServices = [],
            DependencyInjection = new DependencyInjectionInfo(
                Path.Combine(infrastructurePath, "DependencyInjection.cs"),
                []),
        };
    }

    private static ProjectModel CreateWebProjectModel(string rootPath)
    {
        var projectModel = CreateProjectModel(rootPath);
        var webFeaturePath = Path.Combine(Path.GetFullPath(rootPath), "Web", "Features", "Users");

        return new ProjectModel
        {
            Paths = projectModel.Paths,
            Features = projectModel.Features,
            Dtos = projectModel.Dtos,
            DtoSubfolders = projectModel.DtoSubfolders,
            Queries =
            [
                new QueryInfo("GetUsersQuery", "Users", Path.Combine(rootPath, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "Application.Features.Users.Queries"),
            ],
            Commands = projectModel.Commands,
            QueryServices = projectModel.QueryServices,
            Repositories = projectModel.Repositories,
            WebFeatures =
            [
                new WebFeatureInfo("Users", "Users", webFeaturePath),
            ],
            DependencyInjection = projectModel.DependencyInjection,
            Entities = projectModel.Entities,
        };
    }

    private static ArchitectureWarningService CreateArchitectureWarningService()
    {
        return new ArchitectureWarningService(new ArchitectureRuleSet([new SingleQueryServicePerFeatureRule()]));
    }

    private static (GenerationSession Session, IGenerationSessionNavigator Navigator) CreateSessionAndNavigator()
    {
        var session = new GenerationSession();
        var navigator = new GenerationSessionNavigator(session);
        return (session, navigator);
    }

    private static GeneratorDefinitionCatalog CreateEmptyDefinitionCatalog()
    {
        return new GeneratorDefinitionCatalog([]);
    }

    private static GeneratorDefinitionCatalog CreateNestedValidationCatalog()
    {
        return new GeneratorDefinitionCatalog(
        [
            new StubFeatureDefinition(),
            new StubDtoDefinition(),
            new StubEntityDefinition(),
            new StubRepositoryDefinition(),
            new StubQueryDefinition()
        ]);
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class QueryBuildServiceProvider : IServiceProvider
    {
        private readonly CapturingAddQueryPlanService _planService;

        public QueryBuildServiceProvider(CapturingAddQueryPlanService planService)
        {
            _planService = planService;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAddQueryPlanService))
            {
                return _planService;
            }

            if (serviceType == typeof(IQueryServiceSuggestionService))
            {
                return new QueryServiceSuggestionService();
            }

            return null;
        }
    }

    private static WorkspaceGenerationCoordinator CreateCoordinator(
        GenerationSession? generationSession = null,
        GeneratorDefinitionCatalog? definitionCatalog = null,
        IServiceProvider? serviceProvider = null)
    {
        generationSession ??= new GenerationSession();
        definitionCatalog ??= new GeneratorDefinitionCatalog([]);
        serviceProvider ??= new StubServiceProvider();

        return new WorkspaceGenerationCoordinator(
            generationSession,
            new GenerationSessionValidationService(definitionCatalog),
            new GenerationSessionPlanBuilder(definitionCatalog, serviceProvider),
            new PlanPreviewService(new DiffService()),
            new PlanPreparationService());
    }

    private static PreparedApplyPackage PreparePackage(GenerationPlan plan, string targetRootPath)
    {
        return new PlanPreparationService().Prepare(plan, targetRootPath);
    }

    private static WorkspaceStore CreateWorkspaceStore(string rootPath)
    {
        var store = new WorkspaceStore();
        store.SetState(WorkspaceState.Empty with
        {
            TargetRootPath = rootPath,
            Config = GeneratorConfig.ForTargetRoot(rootPath),
            ProjectModel = CreateProjectModel(rootPath),
            IsProjectLoaded = true,
            StatusText = "Project loaded."
        });
        return store;
    }

    private static WorkspaceSessionService CreateSessionService(
        IWorkspaceStore workspaceStore,
        IWorkspaceGenerationCoordinator? coordinator = null,
        IArchitectureWarningService? warningService = null)
    {
        return new WorkspaceSessionService(
            workspaceStore,
            new WorkspaceShellService(new StubProjectOpenService(), new StubProjectScanService()),
            coordinator ?? CreateCoordinator(),
            new StubWorkspaceApplyService(),
            warningService ?? CreateArchitectureWarningService());
    }

    private static IGeneratorCatalog CreateGeneratorCatalog()
    {
        return new GeneratorCatalog(
        [
            new AddQueryScenarioDefinition(() => new AddQueryRootSessionViewModel(
                new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
                new StubAddQueryPlanService(),
                new QueryServiceSuggestionService()))
        ]);
    }

    private sealed class StubThemeService : IThemeService
    {
        public ThemeMode CurrentMode => ThemeMode.System;

        public void Apply(ThemeMode mode)
        {
        }
    }

    private sealed class StubProjectOpenService : IProjectOpenService
    {
        public Task<string?> OpenProjectAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private sealed class FixedProjectOpenService : IProjectOpenService
    {
        private readonly string _targetRootPath;

        public FixedProjectOpenService(string targetRootPath)
        {
            _targetRootPath = targetRootPath;
        }

        public Task<string?> OpenProjectAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(_targetRootPath);
    }

    private sealed class StubProjectScanService : IProjectScanService
    {
        public Task<ProjectScanResult> ScanAsync(string targetRootPath, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectScanResult(GeneratorConfig.ForTargetRoot(targetRootPath), CreateProjectModel(targetRootPath)));
    }

    private sealed class TrackingProjectScanService : IProjectScanService
    {
        public int ScanCount { get; private set; }

        public Task<ProjectScanResult> ScanAsync(string targetRootPath, CancellationToken cancellationToken)
        {
            ScanCount++;
            return Task.FromResult(new ProjectScanResult(GeneratorConfig.ForTargetRoot(targetRootPath), CreateProjectModel(targetRootPath)));
        }
    }

    private sealed class StubAddQueryPlanService : IAddQueryPlanService
    {
        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddQueryFormState formState) => new();
    }

    private sealed class StubCreateFeaturePlanService : ICreateFeaturePlanService
    {
        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, CreateFeatureFormState formState) => new();
    }

    private sealed class CapturingAddQueryPlanServiceWithPlanFactory : IAddQueryPlanService
    {
        private readonly Func<AddQueryFormState, GenerationPlan> _planFactory;

        public CapturingAddQueryPlanServiceWithPlanFactory(Func<AddQueryFormState, GenerationPlan> planFactory)
        {
            _planFactory = planFactory;
        }

        public List<AddQueryFormState> BuiltDrafts { get; } = [];

        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddQueryFormState formState)
        {
            BuiltDrafts.Add(formState);
            return _planFactory(formState);
        }
    }

    private sealed class StubAddDtoPlanService : IAddDtoPlanService
    {
        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddDtoFormState formState) => new();
    }

    private sealed class CapturingAddDtoPlanService : IAddDtoPlanService
    {
        public AddDtoFormState? LastFormState { get; private set; }

        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddDtoFormState formState)
        {
            LastFormState = formState;
            return new GenerationPlan();
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

    private sealed class StubWorkspaceApplyService : IWorkspaceApplyService
    {
        public Task<WorkspaceApplyExecutionResult> ApplyPlanAsync(PreparedApplyPackage package, CancellationToken cancellationToken) =>
            Task.FromResult(WorkspaceApplyExecutionResult.Success("Plan applied."));
    }

    private sealed class TrackingWorkspaceApplyService : IWorkspaceApplyService
    {
        private readonly WorkspaceApplyExecutionResult _result;

        public TrackingWorkspaceApplyService(WorkspaceApplyExecutionResult result)
        {
            _result = result;
        }

        public int ApplyCallCount { get; private set; }

        public Task<WorkspaceApplyExecutionResult> ApplyPlanAsync(PreparedApplyPackage package, CancellationToken cancellationToken)
        {
            ApplyCallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubWorkspaceShellService : IWorkspaceShellService
    {
        private readonly ProjectLoadResult _openProjectResult;
        private readonly ProjectLoadResult _loadProjectResult;

        public StubWorkspaceShellService(
            ProjectLoadResult? openProjectResult = null,
            ProjectLoadResult? loadProjectResult = null)
        {
            _openProjectResult = openProjectResult ?? ProjectLoadResult.Failure("Project open cancelled.", "Target project was not selected.");
            _loadProjectResult = loadProjectResult ?? ProjectLoadResult.Success(
                "Project discovery completed.",
                GeneratorConfig.ForTargetRoot(Path.GetTempPath()),
                CreateProjectModel(Path.GetTempPath()));
        }

        public int OpenProjectCallCount { get; private set; }

        public int LoadProjectCallCount { get; private set; }

        public Task<ProjectLoadResult> OpenProjectAsync(CancellationToken cancellationToken)
        {
            OpenProjectCallCount++;
            return Task.FromResult(_openProjectResult);
        }

        public Task<ProjectLoadResult> LoadProjectAsync(string targetRootPath, CancellationToken cancellationToken)
        {
            LoadProjectCallCount++;
            return Task.FromResult(_loadProjectResult);
        }
    }

    private sealed class StubDialogService : IDialogService
    {
        private readonly bool _confirmResult;

        public StubDialogService(bool confirmResult)
        {
            _confirmResult = confirmResult;
        }

        public Task<bool> ConfirmPlanApplyAsync(PreparedApplyPackage package, CancellationToken cancellationToken) =>
            Task.FromResult(_confirmResult);

        public Task<bool> ConfirmUpdateInstallAsync(string? availableVersion, string message, CancellationToken cancellationToken) =>
            Task.FromResult(_confirmResult);
    }

    private sealed class CapturingDialogService : IDialogService
    {
        private readonly bool _confirmResult;

        public CapturingDialogService(bool confirmResult)
        {
            _confirmResult = confirmResult;
        }

        public PreparedApplyPackage? LastPackage { get; private set; }

        public Task<bool> ConfirmPlanApplyAsync(PreparedApplyPackage package, CancellationToken cancellationToken)
        {
            LastPackage = package;
            return Task.FromResult(_confirmResult);
        }

        public Task<bool> ConfirmUpdateInstallAsync(string? availableVersion, string message, CancellationToken cancellationToken) =>
            Task.FromResult(_confirmResult);
    }

    private static AppUpdateViewModel CreateAppUpdateViewModel(IWorkspaceStore workspaceStore) =>
        new(new StubAppUpdateService(), workspaceStore, new StubDialogService(false));

    private sealed class StubAppUpdateService : IAppUpdateService
    {
        public bool IsSupported => false;

        public string CurrentVersion => "test";

        public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new UpdateCheckResult(false, false, CurrentVersion, null, "Unsupported in tests.", null));

        public Task DownloadUpdatesAsync(UpdateCheckResult update, IProgress<int> progress, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void ApplyUpdatesAndRestart(UpdateCheckResult update)
        {
        }
    }

    private sealed class TrackingArchitectureWarningService : IArchitectureWarningService
    {
        public int RefreshCount { get; private set; }

        public IReadOnlyList<ArchitectureWarning> GetWarnings(ProjectWorkspaceContext context)
        {
            RefreshCount++;
            return [];
        }
    }

    private sealed partial class TestRootSession : ObservableObject, IRootGeneratorSessionViewModel
    {
        public string SessionId => "root:test";

        public GenerationActionDescriptor ActionDescriptor => new(
            "test",
            "Test",
            "Tests",
            "Ready",
            true);

        public string DisplayName => "Test Root";

        public string Summary => "Test root session.";

        public bool IsRoot => true;

        public bool HasUnsavedChanges => false;

        public bool CanClose => true;

        public bool CanBuildPlan => false;

        public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => [];
    }

    private sealed partial class TestMutableRootSession : ObservableObject, IRootGeneratorSessionViewModel
    {
        public string SessionId => "root:mutable";

        public GenerationActionDescriptor ActionDescriptor => new("mutable", "Mutable", "Tests", "Ready", true);

        [ObservableProperty]
        private string _displayName = "Mutable Root";

        public string Summary => "Mutable root session.";

        public bool IsRoot => true;

        public bool HasUnsavedChanges => false;

        public bool CanClose => true;

        public bool CanBuildPlan => false;

        public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => [];
    }

    private sealed partial class TrackingWorkspaceAwareRootSession : ObservableObject, IRootGeneratorSessionViewModel, IWorkspaceAwareGeneratorSessionViewModel
    {
        public string SessionId => "root:tracking";

        public GenerationActionDescriptor ActionDescriptor => new("tracking", "Tracking", "Tests", "Ready", true);

        public string DisplayName => "Tracking Root";

        public string Summary => "Tracks workspace syncs.";

        public bool IsRoot => true;

        public bool HasUnsavedChanges => false;

        public bool CanClose => true;

        public bool CanBuildPlan => false;

        public int UpdateWorkspaceCallCount { get; private set; }

        public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => [];

        public void UpdateWorkspace(ProjectWorkspaceContext? workspaceContext)
        {
            UpdateWorkspaceCallCount++;
        }
    }

    private sealed class UnknownGeneratorSession : IGeneratorSessionViewModel
    {
        public string SessionId => "unknown";

        public string DisplayName => "Unknown";

        public string Summary => "Unknown session.";

        public bool IsRoot => true;

        public bool HasUnsavedChanges => false;

        public bool CanClose => true;
    }

    private sealed partial class TestPlanBuildingRootSession : ObservableObject, IRootGeneratorSessionViewModel, IGeneratorNodeEditorViewModel
    {
        public string SessionId => "root:plan";

        public GenerationActionDescriptor ActionDescriptor => new("plan", "Plan", "Tests", "Ready", true);

        public string DisplayName => "Plan Builder";

        public string Summary => "Builds a test plan.";

        public bool IsRoot => true;

        public bool HasUnsavedChanges => true;

        public bool CanClose => true;

        [ObservableProperty]
        private bool _canBuildPlan = true;

        public GeneratorNode? Node { get; set; }

        public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => [];
    }

    private static TestPlanBuildingRootSession CreateGraphBackedRootSession(
        IGenerationSessionNavigator navigator,
        Func<GenerationPlan> buildPlan,
        string? validationError = null)
    {
        var session = new TestPlanBuildingRootSession();
        session.Node = navigator.CreateRoot(
            GeneratorNodeKind.Query,
            new TestPlanNodeState(buildPlan, validationError));
        return session;
    }

    private sealed class TestPlanDefinition : GeneratorDefinition<TestPlanNodeState>
    {
        public override GeneratorNodeKind Kind => GeneratorNodeKind.Query;

        public override string DisplayName => "Plan";

        public override TestPlanNodeState CreateInitialState(GeneratorCreationContext context)
            => new(() => new GenerationPlan(), null);

        public override IGeneratorNodeEditorViewModel CreateEditor(
            GeneratorNode node,
            TestPlanNodeState state,
            GenerationSession session,
            GeneratorSessionServices services)
            => new TestPlanBuildingRootSession { Node = node };

        public override GeneratorValidationResult Validate(
            GeneratorNode node,
            TestPlanNodeState state,
            GenerationSession session)
            => string.IsNullOrWhiteSpace(state.ValidationError)
                ? GeneratorValidationResult.Valid
                : GeneratorValidationResult.Error(state.ValidationError);

        public override GeneratorPreview BuildPreview(
            GeneratorNode node,
            TestPlanNodeState state,
            GenerationSession session)
            => new(node, "Plan", [], [], [], []);

        public override GenerationPlan BuildPlan(
            GeneratorNode node,
            TestPlanNodeState state,
            GenerationSession session,
            CoreWorkflowContext core)
            => state.BuildPlan();
    }

    private sealed record TestPlanNodeState(Func<GenerationPlan> BuildPlan, string? ValidationError);

    private sealed class StubNodeEditorDefinition : GeneratorDefinition<object>
    {
        public override GeneratorNodeKind Kind => GeneratorNodeKind.Query;

        public override string DisplayName => "Stub Query";

        public override object CreateInitialState(GeneratorCreationContext context) => new();

        public override IGeneratorNodeEditorViewModel CreateEditor(
            GeneratorNode node,
            object state,
            GenerationSession session,
            GeneratorSessionServices services) =>
            throw new NotSupportedException();

        public override GeneratorValidationResult Validate(GeneratorNode node, object state, GenerationSession session) =>
            GeneratorValidationResult.Valid;

        public override GeneratorPreview BuildPreview(GeneratorNode node, object state, GenerationSession session) =>
            new(node, "Stub", [], [], [], []);

        public override GenerationPlan BuildPlan(GeneratorNode node, object state, GenerationSession session, CoreWorkflowContext core) =>
            new();
    }

    private sealed class StubFeatureDefinition : GeneratorDefinition<FeatureGeneratorState>
    {
        public override GeneratorNodeKind Kind => GeneratorNodeKind.Feature;

        public override string DisplayName => "Feature";

        public override FeatureGeneratorState CreateInitialState(GeneratorCreationContext context) => new() { FeatureName = "NewFeature" };

        public override IGeneratorNodeEditorViewModel CreateEditor(GeneratorNode node, FeatureGeneratorState state, GenerationSession session, GeneratorSessionServices services) =>
            throw new NotSupportedException();

        public override GeneratorValidationResult Validate(GeneratorNode node, FeatureGeneratorState state, GenerationSession session) =>
            string.IsNullOrWhiteSpace(state.FeatureName) ? GeneratorValidationResult.Error("Feature name is required.") : GeneratorValidationResult.Valid;

        public override GeneratorPreview BuildPreview(GeneratorNode node, FeatureGeneratorState state, GenerationSession session) =>
            new(node, "Feature", [], [], [], []);

        public override GenerationPlan BuildPlan(GeneratorNode node, FeatureGeneratorState state, GenerationSession session, CoreWorkflowContext core) =>
            new();
    }

    private sealed class StubDtoDefinition : GeneratorDefinition<DtoGeneratorState>
    {
        public override GeneratorNodeKind Kind => GeneratorNodeKind.Dto;

        public override string DisplayName => "Dto";

        public override DtoGeneratorState CreateInitialState(GeneratorCreationContext context) => new() { BaseName = "NewDto" };

        public override IGeneratorNodeEditorViewModel CreateEditor(GeneratorNode node, DtoGeneratorState state, GenerationSession session, GeneratorSessionServices services) =>
            throw new NotSupportedException();

        public override GeneratorValidationResult Validate(GeneratorNode node, DtoGeneratorState state, GenerationSession session) =>
            string.IsNullOrWhiteSpace(state.BaseName) ? GeneratorValidationResult.Error("DTO name is required.") : GeneratorValidationResult.Valid;

        public override GeneratorPreview BuildPreview(GeneratorNode node, DtoGeneratorState state, GenerationSession session) =>
            new(node, "Dto", [], [], [], []);

        public override GenerationPlan BuildPlan(GeneratorNode node, DtoGeneratorState state, GenerationSession session, CoreWorkflowContext core) =>
            new();
    }

    private sealed class StubEntityDefinition : GeneratorDefinition<EntityGeneratorState>
    {
        public override GeneratorNodeKind Kind => GeneratorNodeKind.Entity;

        public override string DisplayName => "Entity";

        public override EntityGeneratorState CreateInitialState(GeneratorCreationContext context) => new() { EntityName = "NewEntity" };

        public override IGeneratorNodeEditorViewModel CreateEditor(GeneratorNode node, EntityGeneratorState state, GenerationSession session, GeneratorSessionServices services) =>
            throw new NotSupportedException();

        public override GeneratorValidationResult Validate(GeneratorNode node, EntityGeneratorState state, GenerationSession session) =>
            string.IsNullOrWhiteSpace(state.EntityName) ? GeneratorValidationResult.Error("Entity name is required.") : GeneratorValidationResult.Valid;

        public override GeneratorPreview BuildPreview(GeneratorNode node, EntityGeneratorState state, GenerationSession session) =>
            new(node, "Entity", [], [], [], []);

        public override GenerationPlan BuildPlan(GeneratorNode node, EntityGeneratorState state, GenerationSession session, CoreWorkflowContext core) =>
            new();
    }

    private sealed class StubRepositoryDefinition : GeneratorDefinition<RepositoryGeneratorState>
    {
        public override GeneratorNodeKind Kind => GeneratorNodeKind.Repository;

        public override string DisplayName => "Repository";

        public override RepositoryGeneratorState CreateInitialState(GeneratorCreationContext context) => new() { InterfaceName = "IRepository" };

        public override IGeneratorNodeEditorViewModel CreateEditor(GeneratorNode node, RepositoryGeneratorState state, GenerationSession session, GeneratorSessionServices services) =>
            throw new NotSupportedException();

        public override GeneratorValidationResult Validate(GeneratorNode node, RepositoryGeneratorState state, GenerationSession session) =>
            string.IsNullOrWhiteSpace(state.InterfaceName) ? GeneratorValidationResult.Error("Repository name is required.") : GeneratorValidationResult.Valid;

        public override GeneratorPreview BuildPreview(GeneratorNode node, RepositoryGeneratorState state, GenerationSession session) =>
            new(node, "Repository", [], [], [], []);

        public override GenerationPlan BuildPlan(GeneratorNode node, RepositoryGeneratorState state, GenerationSession session, CoreWorkflowContext core) =>
            new();
    }

    private sealed class StubQueryDefinition : GeneratorDefinition<QueryGeneratorState>
    {
        public override GeneratorNodeKind Kind => GeneratorNodeKind.Query;

        public override string DisplayName => "Query";

        public override QueryGeneratorState CreateInitialState(GeneratorCreationContext context) => new() { QueryName = "Get" };

        public override IGeneratorNodeEditorViewModel CreateEditor(GeneratorNode node, QueryGeneratorState state, GenerationSession session, GeneratorSessionServices services) =>
            throw new NotSupportedException();

        public override GeneratorValidationResult Validate(GeneratorNode node, QueryGeneratorState state, GenerationSession session) =>
            string.IsNullOrWhiteSpace(state.QueryName) ? GeneratorValidationResult.Error("Query name is required.") : GeneratorValidationResult.Valid;

        public override GeneratorPreview BuildPreview(GeneratorNode node, QueryGeneratorState state, GenerationSession session) =>
            new(node, "Query", [], [], [], []);

        public override GenerationPlan BuildPlan(GeneratorNode node, QueryGeneratorState state, GenerationSession session, CoreWorkflowContext core) =>
            new();
    }

    [Fact]
    public void MultiSelectListPicker_SetDiscovered_PopulatesFilteredItems()
    {
        var picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
        };

        picker.SetDiscovered(new List<string> { "ICurrentUserContext", "IUnitOfWork" });

        var items = picker.FilteredItems.Cast<WrappedListItem>().ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.DisplayText == "ICurrentUserContext");
        Assert.Contains(items, i => i.DisplayText == "IUnitOfWork");
        Assert.All(items, i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void MultiSelectListPicker_SelectAllAndClearSelection_UpdateSelectedKeys()
    {
        var picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item?.ToString() ?? string.Empty,
        };

        picker.SetDiscovered(new List<string> { "Id", "Name" });

        picker.SelectAllCommand.Execute(null);
        Assert.Equal(2, picker.SelectedKeys.Count);

        picker.DeselectAllCommand.Execute(null);
        Assert.Empty(picker.SelectedKeys);
    }

    [Fact]
    public void DependencyPicker_SetDiscovered_PopulatesPicker()
    {
        var depPicker = new DependencyPickerViewModel();

        depPicker.SetDiscovered(Array.Empty<RepositoryInfo>());

        var items = depPicker.Picker.FilteredItems.Cast<WrappedListItem>().ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.DisplayText == "ICurrentUserContext");
        Assert.Contains(items, i => i.DisplayText == "IUnitOfWork");
        Assert.All(items, i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void DependencyPicker_GetSelected_ReturnsEmptyByDefault()
    {
        var depPicker = new DependencyPickerViewModel();
        depPicker.SetDiscovered(Array.Empty<RepositoryInfo>());

        var selected = depPicker.GetSelected();

        Assert.Empty(selected);
    }

    [Fact]
    public void DtoRootSession_UsesDiscoveredDtoSubfoldersFromProjectModel()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));

        var session = new DtoRootSessionViewModel(
            actionDescriptor: null,
            new StubAddDtoPlanService(),
            isStandalone: true);
        session.SetGenerationSession(new GenerationSession());

        session.UpdateWorkspace(projectContext);

        var items = session.SubfolderPicker.Items.Cast<string>().ToList();
        Assert.Contains("(root folder)", items);
        Assert.Contains("Admin", items);
    }

    [Fact]
    public void WrappedListPicker_CustomEntrySearch_StaysVisibleAndUpdatesPrimaryText()
    {
        var picker = new WrappedListPickerViewModel
        {
            AllowCustom = true,
            CustomEntryLabel = "Custom...",
            CustomEntryPrefix = "",
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
        };
        picker.Items = new List<string> { "UserDto", "OrderDto" };

        picker.SearchText = "LookupResult";

        var customItem = Assert.Single(picker.FilteredItems.Cast<WrappedListItem>());
        Assert.True(customItem.IsCustom);
        Assert.Equal("LookupResult", customItem.BaseName);
        Assert.Equal("LookupResult", customItem.SearchText);
        Assert.Equal("LookupResult", customItem.PrimaryText);
    }

    [Fact]
    public void WrappedListPicker_CustomEntry_CyclePrefixUpdatesPrimaryText()
    {
        var picker = new WrappedListPickerViewModel
        {
            AllowCustom = true,
            CustomEntryLabel = "Custom...",
            Prefixes = ["", GeneratorConstants.ListWrapperPrefix, GeneratorConstants.EnumerableWrapperPrefix],
            Suffixes = [GeneratorConstants.GenericWrapperSuffix],
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
        };
        picker.Items = new List<string> { "UserDto" };
        picker.SearchText = "LookupResult";

        picker.CyclePrefixForwardCommand.Execute(null);

        var customItem = Assert.Single(picker.FilteredItems.Cast<WrappedListItem>());
        Assert.True(customItem.IsCustom);
        Assert.Equal("List<LookupResult>", customItem.PrimaryText);

        picker.CyclePrefixForwardCommand.Execute(null);

        customItem = Assert.Single(picker.FilteredItems.Cast<WrappedListItem>());
        Assert.Equal("IEnumerable<LookupResult>", customItem.PrimaryText);
    }

    [Fact]
    public void WrappedListPicker_RegularEntry_CyclePrefixUpdatesPrimaryText()
    {
        var picker = new WrappedListPickerViewModel
        {
            Prefixes = ["", GeneratorConstants.ListWrapperPrefix, GeneratorConstants.EnumerableWrapperPrefix],
            Suffixes = [GeneratorConstants.GenericWrapperSuffix],
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
        };
        picker.Items = new List<string> { "UserDto" };

        picker.CyclePrefixForwardCommand.Execute(null);

        var item = Assert.Single(picker.FilteredItems.Cast<WrappedListItem>());
        Assert.Equal("List<UserDto>", item.PrimaryText);

        picker.CyclePrefixForwardCommand.Execute(null);

        item = Assert.Single(picker.FilteredItems.Cast<WrappedListItem>());
        Assert.Equal("IEnumerable<UserDto>", item.PrimaryText);
    }

    [Fact]
    public void CyclicInput_SetFullText_SeparatesKnownPrefixAndPreservesCustomVerb()
    {
        var input = new CyclicInputViewModel
        {
            Prefixes = ["", .. GeneratorConstants.CommandVerbPrefixes],
            Suffixes = [""],
            SelectedIndex = 1,
        };

        input.SetFullText("UpdateUser");

        Assert.Equal("Update", input.CurrentPrefix);
        Assert.Equal("User", input.Text);
        Assert.Equal("UpdateUser", input.FullText);

        input.SetFullText("ActivateUser");

        Assert.Equal("", input.CurrentPrefix);
        Assert.Equal("ActivateUser", input.Text);
        Assert.Equal("ActivateUser", input.FullText);
    }

    [Fact]
    public void CommandRootSession_OffersAllVerbPrefixesAndRestoresSavedName()
    {
        var state = new CommandGeneratorState { CommandName = "DeleteUser" };
        var node = new GeneratorNode
        {
            Kind = GeneratorNodeKind.Command,
            State = state,
        };

        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubAddCommandPlanService(),
            node: node);

        Assert.Equal(["", "Create", "Update", "Delete"], session.CommandNameCyclic.Prefixes);
        Assert.Equal("Delete", session.CommandNameCyclic.CurrentPrefix);
        Assert.Equal("User", session.CommandNameCyclic.Text);
        Assert.Equal("DeleteUser", session.CommandName);
        Assert.Equal("DeleteUser", state.CommandName);
        var id = Assert.Single(session.Parameters);
        Assert.Equal("int", id.Type);
        Assert.Equal("Id", id.Name);
        Assert.Equal(new PropertySpec("int", "Id"), Assert.Single(state.Parameters));
    }

    [Fact]
    public void CommandRootSession_DeleteWithExistingParameters_DoesNotAddOrLoseParameters()
    {
        var state = new CommandGeneratorState { CommandName = "DeleteUser" };
        state.Parameters.Add(new PropertySpec("string", "ExternalKey"));
        var node = new GeneratorNode
        {
            Kind = GeneratorNodeKind.Command,
            State = state,
        };

        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubAddCommandPlanService(),
            node: node);

        var parameter = Assert.Single(session.Parameters);
        Assert.Equal("string", parameter.Type);
        Assert.Equal("ExternalKey", parameter.Name);
        Assert.Equal(new PropertySpec("string", "ExternalKey"), Assert.Single(state.Parameters));
    }

    [Fact]
    public void CommandRootSession_SwitchingEmptyCommandToDelete_AddsId()
    {
        var state = new CommandGeneratorState { CommandName = "CreateUser" };
        var node = new GeneratorNode
        {
            Kind = GeneratorNodeKind.Command,
            State = state,
        };
        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubAddCommandPlanService(),
            node: node);

        session.CommandNameCyclic.SetFullText("DeleteUser");

        var parameter = Assert.Single(session.Parameters);
        Assert.Equal("int", parameter.Type);
        Assert.Equal("Id", parameter.Name);
    }

    [Fact]
    public void CommandRootSession_RestoresAndPersistsGenerateHandlerBody()
    {
        var state = new CommandGeneratorState
        {
            CommandName = "CreateUser",
            GenerateHandlerBody = false,
        };
        var node = new GeneratorNode
        {
            Kind = GeneratorNodeKind.Command,
            State = state,
        };
        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubAddCommandPlanService(),
            node: node);

        Assert.False(session.GenerateHandlerBody);

        session.GenerateHandlerBody = true;

        Assert.True(state.GenerateHandlerBody);
    }

    [Fact]
    public void QueryRootSession_OffersAllVerbPrefixesAndRestoresSavedName()
    {
        var state = new QueryGeneratorState { QueryName = "SearchUsers" };
        var node = new GeneratorNode
        {
            Kind = GeneratorNodeKind.Query,
            State = state,
        };

        var session = new AddQueryRootSessionViewModel(
            actionDescriptor: null,
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService(),
            node: node);

        Assert.Equal(["", "Get", "Find", "Fetch", "Search", "List", "Load"], session.QueryNameCyclic.Prefixes);
        Assert.Equal("Search", session.QueryNameCyclic.CurrentPrefix);
        Assert.Equal("Users", session.QueryNameCyclic.Text);
        Assert.Equal("SearchUsers", session.QueryName);
        Assert.Equal("SearchUsers", state.QueryName);
    }



    [Fact]
    public void CreateFeatureRootSession_Reopen_PreservesSelectedListedSubfolder()
    {
        using var tmp = new TempProject();
        var baseProjectModel = CreateProjectModel(tmp.Root);
        var projectModel = new ProjectModel
        {
            Paths = baseProjectModel.Paths,
            Features =
            [
                .. baseProjectModel.Features,
                new FeatureInfo("Invoices", "Admin/Invoices", Path.Combine(tmp.Root, "Application", "Features", "Admin", "Invoices"))
            ],
            Dtos = baseProjectModel.Dtos,
            DtoSubfolders = baseProjectModel.DtoSubfolders,
            Queries = baseProjectModel.Queries,
            Commands = baseProjectModel.Commands,
            QueryServices = baseProjectModel.QueryServices,
            Repositories = baseProjectModel.Repositories,
            WebFeatures = baseProjectModel.WebFeatures,
            DependencyInjection = baseProjectModel.DependencyInjection,
            Entities = baseProjectModel.Entities
        };
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            projectModel);
        var node = new GeneratorNode
        {
            Kind = GeneratorNodeKind.Feature,
            State = new FeatureGeneratorState { FeatureName = "Invoices" }
        };

        var firstSession = new CreateFeatureRootSessionViewModel(
            actionDescriptor: null,
            new StubCreateFeaturePlanService(),
            isStandalone: false,
            node: node);
        firstSession.UpdateWorkspace(projectContext);
        var adminItem = firstSession.SubfolderPicker.Items.Cast<string>().First(item => item == "Admin");
        firstSession.SubfolderPicker.SelectRawItem(adminItem);

        var state = Assert.IsType<FeatureGeneratorState>(node.State);
        Assert.Equal("Admin", state.Subfolder);

        var reopenedSession = new CreateFeatureRootSessionViewModel(
            actionDescriptor: null,
            new StubCreateFeaturePlanService(),
            isStandalone: false,
            node: node);
        reopenedSession.UpdateWorkspace(projectContext);

        Assert.Equal("Admin", reopenedSession.SubfolderPicker.SelectedRawItem);
    }

    [Fact]
    public void CommandRootSession_ScenarioOutline_ReflectsSessionGraph()
    {
        var (genSession, navigator) = CreateSessionAndNavigator();
        var commandNode = navigator.CreateRoot(GeneratorNodeKind.Command, new CommandGeneratorState
        {
            CommandName = "GetUsers",
        });

        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubAddCommandPlanService(),
            node: commandNode);

        var nodes = session.GetScenarioNodes();

        Assert.Single(nodes);
        Assert.Equal("Command", nodes[0].Title);
        Assert.Equal("Command", nodes[0].Summary);
        Assert.Empty(nodes[0].Children);
    }

    [Fact]
    public void NestedGenerator_UI_ShowsDoneCancelNotBack()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var sessionService = CreateSessionService(workspaceStore);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var host = new GeneratorHostViewModel(
            workspaceStore,
            CreateGeneratorCatalog(),
            sessionService,
            genSession,
            navigator,
            CreateNestedValidationCatalog(),
            new StubServiceProvider());
        var rootSession = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        host.Stack.OpenRoot(rootSession);

        Assert.False(host.ShowDoneButton);
        Assert.False(host.ShowCancelButton);
        Assert.True(host.ShowCloseButton);
        Assert.False(host.ShowBackButton);

        rootSession.OpenCreateFeatureCommand.Execute(null);

        Assert.True(host.ShowDoneButton);
        Assert.True(host.ShowCancelButton);
        Assert.True(host.ShowCloseButton);
        Assert.False(host.ShowBackButton);
    }

    [Fact]
    public void AddQuery_CreateFeature_Done_CommitsAndSelectsFeature()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        stack.OpenRoot(session);
        var queryState = Assert.IsType<QueryGeneratorState>(session.Node!.State);
        var originalFeatureRef = queryState.FeatureRef;
        session.OpenCreateFeatureCommand.Execute(null);

        var featureNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        Assert.Equal(GeneratorNodeKind.Feature, featureNode.Kind);

        stack.DoneNestedCommand.Execute(null);

        Assert.Equal(session.Node!.Id, genSession.ActiveNode?.Id);
        Assert.Equal(GeneratorNodeLifecycle.Committed, featureNode.Lifecycle);
        Assert.NotEqual(originalFeatureRef?.NodeId, queryState.FeatureRef?.NodeId);
        Assert.Equal(featureNode.Id, queryState.FeatureRef?.NodeId);
        Assert.Equal(featureNode.Id, session.SelectedFeature?.NodeId);
    }

    [Fact]
    public void AddQuery_CreateFeature_Cancel_DoesNotCommitFeature()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        stack.OpenRoot(session);
        var queryState = Assert.IsType<QueryGeneratorState>(session.Node!.State);
        var originalFeatureRef = queryState.FeatureRef;
        session.OpenCreateFeatureCommand.Execute(null);

        var featureNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        Assert.Equal(GeneratorNodeKind.Feature, featureNode.Kind);

        stack.CancelNestedCommand.Execute(null);

        Assert.Null(genSession.FindNode(featureNode.Id));
        Assert.Equal(session.Node!.Id, genSession.ActiveNode?.Id);
        Assert.Equal(originalFeatureRef?.NodeId, queryState.FeatureRef?.NodeId);
        Assert.DoesNotContain(session.FeatureItems, feature => feature.NodeId == featureNode.Id);
    }

    [Fact]
    public void AddQuery_CreateDto_WithProjectFeature_PreselectsFeature()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(tmp.Root, GeneratorConfig.ForTargetRoot(tmp.Root), CreateProjectModel(tmp.Root));
        var (genSession, navigator) = CreateSessionAndNavigator();
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());
        session.SetGenerationSession(genSession, navigator);
        session.UpdateWorkspace(projectContext);
        Assert.NotNull(session.SelectedFeature);
        var selectedFeature = session.SelectedFeature!;

        session.OpenCreateDtoCommand.Execute(null);

        var dtoNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        var dtoSession = new DtoRootSessionViewModel(
            actionDescriptor: null,
            new StubAddDtoPlanService(),
            isStandalone: false,
            node: dtoNode);
        dtoSession.SetGenerationSession(genSession);
        dtoSession.UpdateWorkspace(projectContext);

        Assert.Equal(selectedFeature.RelativePath, dtoSession.SelectedFeature?.RelativePath);
        Assert.Equal(selectedFeature.Ref?.NodeId, dtoSession.SelectedFeature?.Ref?.NodeId);
    }

    [Fact]
    public void AddQuery_CreateDto_WithSessionFeature_PreselectsFeature()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var projectContext = workspaceStore.State.ProjectContext!;
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var querySession = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());
        stack.OpenRoot(querySession);
        querySession.OpenCreateFeatureCommand.Execute(null);
        var featureNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        stack.DoneNestedCommand.Execute(null);

        querySession.OpenCreateDtoCommand.Execute(null);

        var dtoNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        var dtoSession = new DtoRootSessionViewModel(
            actionDescriptor: null,
            new StubAddDtoPlanService(),
            isStandalone: false,
            node: dtoNode);
        dtoSession.SetGenerationSession(genSession);
        dtoSession.UpdateWorkspace(projectContext);

        Assert.Equal(featureNode.Id, dtoSession.SelectedFeature?.NodeId);
        Assert.True(dtoSession.SelectedFeature?.IsFromSession);
    }

    [Fact]
    public void AddQuery_CreateDto_Done_CommitsResultDto()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        stack.OpenRoot(session);
        session.OpenCreateDtoCommand.Execute(null);

        var dtoNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        stack.DoneNestedCommand.Execute(null);

        var queryState = Assert.IsType<QueryGeneratorState>(session.Node!.State);
        Assert.Equal(GeneratorNodeLifecycle.Committed, dtoNode.Lifecycle);
        Assert.Equal(session.Node!.Id, genSession.ActiveNode?.Id);
        Assert.Equal(dtoNode.Id, queryState.ResultDtoRef?.NodeId);
        Assert.Equal(dtoNode.Id, session.SelectedDtoChoice?.NodeId);
    }

    [Fact]
    public void AddQuery_CreateDto_Cancel_DoesNotCommitResultDto()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubAddQueryPlanService(),
            new QueryServiceSuggestionService());

        stack.OpenRoot(session);
        var queryState = Assert.IsType<QueryGeneratorState>(session.Node!.State);
        var originalDtoRef = queryState.ResultDtoRef;
        session.OpenCreateDtoCommand.Execute(null);

        var dtoNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        stack.CancelNestedCommand.Execute(null);

        Assert.Null(genSession.FindNode(dtoNode.Id));
        Assert.Equal(session.Node!.Id, genSession.ActiveNode?.Id);
        Assert.Equal(originalDtoRef?.NodeId, queryState.ResultDtoRef?.NodeId);
        Assert.DoesNotContain(session.ResultTypeItems, dto => dto.NodeId == dtoNode.Id);
    }

    [Fact]
    public void Repository_CreateEntity_Done_CommitsEntity()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new RepositoryRootSessionViewModel(
            new GenerationActionDescriptor("add-repository", "Add Repository", "Application", "Ready", true),
            new StubAddRepositoryPlanService(),
            isStandalone: true);

        stack.OpenRoot(session);
        session.OpenCreateEntityCommand.Execute(null);

        var entityNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        stack.DoneNestedCommand.Execute(null);

        var repositoryState = Assert.IsType<RepositoryGeneratorState>(session.Node!.State);
        Assert.Equal(GeneratorNodeLifecycle.Committed, entityNode.Lifecycle);
        Assert.Equal(entityNode.Id, repositoryState.EntityRef?.NodeId);
        Assert.Equal(entityNode.Id, session.SelectedEntity?.NodeId);
    }

    [Fact]
    public void Repository_CreateEntity_Cancel_DoesNotCommitEntity()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new RepositoryRootSessionViewModel(
            new GenerationActionDescriptor("add-repository", "Add Repository", "Application", "Ready", true),
            new StubAddRepositoryPlanService(),
            isStandalone: true);

        stack.OpenRoot(session);
        var originalEntityRef = Assert.IsType<RepositoryGeneratorState>(session.Node!.State).EntityRef;
        session.OpenCreateEntityCommand.Execute(null);

        var entityNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        stack.CancelNestedCommand.Execute(null);

        var repositoryState = Assert.IsType<RepositoryGeneratorState>(session.Node!.State);
        Assert.Null(genSession.FindNode(entityNode.Id));
        Assert.Equal(originalEntityRef?.NodeId, repositoryState.EntityRef?.NodeId);
        Assert.DoesNotContain(session.AvailableEntities, entity => entity.NodeId == entityNode.Id);
    }

    [Fact]
    public void Command_CreateRepository_Done_AddsRepositoryRef()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubAddCommandPlanService());

        stack.OpenRoot(session);
        session.DependencyPicker.CreateRepositoryCommand!.Execute(null);

        var repositoryNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        stack.DoneNestedCommand.Execute(null);

        var commandState = Assert.IsType<CommandGeneratorState>(session.Node!.State);
        Assert.Contains(commandState.RepositoryRefs, reference => reference.NodeId == repositoryNode.Id);
        Assert.Contains(session.DependencyPicker.GetSelectedOptions(), option => option.NodeId == repositoryNode.Id);
    }

    [Fact]
    public void Command_CreateRepository_Cancel_DoesNotAddRepositoryRef()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var (genSession, navigator) = CreateSessionAndNavigator();
        var stack = new GeneratorStackViewModel(workspaceStore, genSession, navigator, CreateNestedValidationCatalog(), new StubServiceProvider());
        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubAddCommandPlanService());

        stack.OpenRoot(session);
        session.DependencyPicker.CreateRepositoryCommand!.Execute(null);

        var repositoryNode = Assert.IsType<GeneratorNode>(genSession.ActiveNode);
        stack.CancelNestedCommand.Execute(null);

        var commandState = Assert.IsType<CommandGeneratorState>(session.Node!.State);
        Assert.Null(genSession.FindNode(repositoryNode.Id));
        Assert.DoesNotContain(commandState.RepositoryRefs, reference => reference.NodeId == repositoryNode.Id);
        Assert.DoesNotContain(session.DependencyPicker.GetSelectedOptions(), option => option.NodeId == repositoryNode.Id);
    }



    [Fact]
    public void EntityRootSession_EfEntitySelection_DoesNotAutoSelectFields()
    {
        var session = CreateEntitySession();
        session.UseEfEntity = true;
        session.SelectedEfEntity = CreateEfEntityCandidate("Customer");

        Assert.Empty(session.EfPropertyPicker.SelectedKeys);
    }

    [Fact]
    public void EntityRootSession_ChangingEfEntity_UpdatesEntityNameUntilManualOverride()
    {
        var session = CreateEntitySession();
        session.UseEfEntity = true;

        session.SelectedEfEntity = CreateEfEntityCandidate("Customer");
        Assert.Equal("Customer", session.EntityName);

        session.SelectedEfEntity = CreateEfEntityCandidate("Order");
        Assert.Equal("Order", session.EntityName);

        session.EntityNameCyclic.Text = "ManualOrder";
        session.SelectedEfEntity = CreateEfEntityCandidate("Invoice");

        Assert.Equal("ManualOrder", session.EntityName);
    }

    [Fact]
    public void EntityRootSession_DisablesInterfaceAndDomainMethodsForGui()
    {
        var session = CreateEntitySession();
        session.EntityNameCyclic.Text = "Customer";
        session.ManualProperties[0].Type = "string";
        session.ManualProperties[0].Name = "Name";
        session.GenerateFactoryMethod = true;
        session.GenerateEfMapping = true;

        Assert.True(session.CanComplete);
    }

    private static EntityRootSessionViewModel CreateEntitySession()
    {
        return new EntityRootSessionViewModel(
            actionDescriptor: null,
            new StubAddEntityPlanService(),
            new EfEntityPreparationService(),
            isStandalone: false);
    }

    private static EfEntityCandidate CreateEfEntityCandidate(string name)
    {
        return new EfEntityCandidate(
            name,
            $"/tmp/{name}.cs",
            [
                new EfEntityProperty("Id" + name, "Int32"),
                new EfEntityProperty("DisplayName", "String"),
            ]);
    }

    private sealed class StubAddCommandPlanService : IAddCommandPlanService
    {
        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddCommandFormState formState)
        {
            return new GenerationPlan();
        }
    }

    private sealed class CapturingAddCommandPlanService : IAddCommandPlanService
    {
        public AddCommandFormState? LastFormState { get; private set; }

        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddCommandFormState formState)
        {
            LastFormState = formState;
            return new GenerationPlan();
        }
    }

    private sealed class StubAddRepositoryPlanService : IAddRepositoryPlanService
    {
        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddRepositoryFormState formState)
        {
            return new GenerationPlan();
        }
    }

    private sealed class StubAddEntityPlanService : IAddEntityPlanService
    {
        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddEntityFormState formState)
        {
            return new GenerationPlan();
        }
    }
}
