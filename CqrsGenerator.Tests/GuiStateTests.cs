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
using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;
using CqrsGenerator.Gui.Views;

namespace CqrsGenerator.Tests;

public partial class GuiStateTests
{
    [Fact]
    public void EmbeddedCompletionReactivity_UpdatesWithoutManualRefresh()
    {
        var workspaceStore = new WorkspaceStore();
        var stack = new GeneratorStackViewModel(workspaceStore);
        var rootSession = new TestRootSession();

        stack.OpenRoot(rootSession);

        var embedded = new DtoRootSessionViewModel(
            actionDescriptor: null,
            new StubAddDtoPlanService(),
            new StubEmbeddedSessionHost(),
            new AddDtoScenarioOutlineBuilder(),
            isStandalone: false);
        stack.Open<NewDtoDraft>(embedded, _ => { });

        Assert.False(stack.CanCompleteEmbedded);

        embedded.DtoNameCyclic.Text = "UserDetails";
        embedded.DtoNameCyclic.SelectedIndex = 1;

        Assert.True(embedded.CanComplete);
        Assert.True(stack.CanCompleteEmbedded);
        Assert.True(stack.CompleteEmbeddedCommand.CanExecute(null));
    }

    [Fact]
    public void AddQuery_RemoveParameterCommand_RemovesSpecificEntry()
    {
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubEmbeddedSessionHost(),
            new StubAddQueryPlanService(),
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());

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
            new StubEmbeddedSessionHost(),
            new AddDtoScenarioOutlineBuilder(),
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
        var stack = new GeneratorStackViewModel(workspaceStore);
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
        var stack = new GeneratorStackViewModel(workspaceStore);
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
    public async Task BuildPlanSuccess_UpdatesWorkspaceStateAndPreviewSnapshot()
    {
        using var tmp = new TempProject();
        var workspaceStore = CreateWorkspaceStore(tmp.Root);
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs"), "public class GetUsersQuery {}");

        var coordinator = CreateCoordinator();
        var sessionService = CreateSessionService(workspaceStore, coordinator);
        var session = new TestPlanBuildingRootSession(() => plan);

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

        var sessionService = CreateSessionService(workspaceStore, CreateCoordinator());
        var session = new TestPlanBuildingRootSession(() => plan);

        await sessionService.BuildPlanAsync(session, CancellationToken.None);

        Assert.Single(workspaceStore.State.GenerationWarnings);
        Assert.Equal("Manual validation is required.", workspaceStore.State.GenerationWarnings[0].Message);
        Assert.Single(workspaceStore.State.CurrentPlanPreviewSnapshot.GenerationWarnings);
        Assert.Equal(
            workspaceStore.State.GenerationWarnings[0].Message,
            workspaceStore.State.CurrentPlanPreviewSnapshot.GenerationWarnings[0].Message);
    }

    [Fact]
    public async Task BuildPlanFailure_ClearsPlanAndPublishesErrorState()
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

        var coordinator = CreateCoordinator();
        var sessionService = CreateSessionService(workspaceStore, coordinator);
        var session = new TestPlanBuildingRootSession(() => throw new InvalidOperationException("Exploded."));

        await sessionService.BuildPlanAsync(session, CancellationToken.None);

        Assert.Null(workspaceStore.State.CurrentPlan);
        Assert.Null(workspaceStore.State.CurrentPreparedApplyPackage);
        Assert.Empty(workspaceStore.State.CurrentPlanPreviewSnapshot.RootNodes);
        Assert.Equal(PlanBuildStatus.Failed, workspaceStore.State.PlanBuildStatus);
        Assert.Equal("Exploded.", workspaceStore.State.PlanBuildErrorMessage);
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
    public void MainWindow_BuildAvailabilityTracksRootValidationState()
    {
        var warningService = CreateArchitectureWarningService();
        var workspaceStore = CreateWorkspaceStore(Path.GetTempPath());
        var coordinator = CreateCoordinator();
        var sessionService = CreateSessionService(workspaceStore, coordinator, warningService);
        var generatorCatalog = CreateGeneratorCatalog();
        var generatorHost = new GeneratorHostViewModel(workspaceStore, generatorCatalog, sessionService);
        var mainWindow = new MainWindowViewModel(
            new StubThemeService(),
            workspaceStore,
            sessionService,
            generatorHost);

        var action = mainWindow.GeneratorHost.ActionLauncher.Groups
            .SelectMany(group => group.Actions)
            .Single(item => item.Action.ActionId == "add-query");

        action.OpenCommand.Execute(null);

        Assert.True(mainWindow.BuildPlanCommand.CanExecute(null));
        Assert.True(mainWindow.MessagesPanel.CanBuildPlan);

        var rootSession = Assert.IsType<AddQueryRootSessionViewModel>(mainWindow.GeneratorHost.Stack.RootSession);

        rootSession.QueryName = string.Empty;

        Assert.False(rootSession.CanBuildPlan);
        Assert.False(mainWindow.BuildPlanCommand.CanExecute(null));
        Assert.False(mainWindow.MessagesPanel.CanBuildPlan);
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
        var generatorHost = new GeneratorHostViewModel(workspaceStore, generatorCatalog, sessionService);
        var mainWindow = new MainWindowViewModel(
            new StubThemeService(),
            workspaceStore,
            sessionService,
            generatorHost);

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
    public void AddQuery_UsesDtoNameForGenerationButKeepsDisplayNameForUi()
    {
        using var tmp = new TempProject();
        var featurePath = Path.Combine(tmp.Root, "Application", "Features", "Users");
        var projectModel = new ProjectModel
        {
            Paths = new ProjectPaths(
                tmp.Root,
                Path.Combine(tmp.Root, "Application"),
                Path.Combine(tmp.Root, "Application", "Features"),
                Path.Combine(tmp.Root, "Infrastructure"),
                Path.Combine(tmp.Root, "Infrastructure", "Data", "QueryServices"),
                Path.Combine(tmp.Root, "Infrastructure", "Data", "Repositories"),
                Path.Combine(tmp.Root, "Infrastructure", "DependencyInjection.cs"),
                Path.Combine(tmp.Root, "Web"),
                Path.Combine(tmp.Root, "Web", "Features")),
            Features =
            [
                new FeatureInfo("Users", "Users", featurePath),
            ],
            Dtos =
            [
                new DtoInfo(
                    "CategoryDto",
                    "Users",
                    Path.Combine(featurePath, "DTOs", "Admin", "CategoryDto.cs"),
                    "Application.Features.Users.DTOs.Admin",
                    "Admin/CategoryDto"),
            ],
            QueryServices = [],
            DependencyInjection = new DependencyInjectionInfo(
                Path.Combine(tmp.Root, "Infrastructure", "DependencyInjection.cs"),
                []),
        };
        var projectContext = new ProjectWorkspaceContext(tmp.Root, GeneratorConfig.ForTargetRoot(tmp.Root), projectModel);

        var action = new GenerationActionDescriptor(
            "add-query",
            "Add Query",
            "Application",
            "Ready",
            true);

        var capturedPlanService = new CapturingAddQueryPlanService();
        var session = new AddQueryRootSessionViewModel(
            action,
            new StubEmbeddedSessionHost(),
            capturedPlanService,
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());
        session.UpdateWorkspace(projectContext);
        session.QueryName = "GetTest";

        var selectedDto = Assert.Single(session.ResultTypeItems);
        Assert.Equal("Admin/CategoryDto", selectedDto.DisplayName);
        Assert.Equal("CategoryDto", selectedDto.Name);

        session.BuildPlan(projectContext);

        Assert.Equal("CategoryDto", capturedPlanService.LastFormState!.ResultTypeName);
    }

    [Fact]
    public void AddQuery_MethodNameAutoDerivesFromQueryNameAndUsesSwitchableAsyncSuffix()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var capturedPlanService = new CapturingAddQueryPlanService();
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubEmbeddedSessionHost(),
            capturedPlanService,
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());
        session.UpdateWorkspace(projectContext);
        session.QueryName = "GetUsers";

        Assert.Equal("GetUsers", session.MethodNameCyclic.Text);
        Assert.Equal(1, session.MethodNameCyclic.SelectedIndex);
        Assert.Equal("GetUsersAsync", session.MethodNameCyclic.FullText);

        session.BuildPlan(projectContext);
        Assert.Equal("GetUsersAsync", capturedPlanService.LastFormState!.MethodName);

        session.MethodNameCyclic.SelectedIndex = 0;

        Assert.Equal("GetUsers", session.MethodNameCyclic.FullText);

        session.BuildPlan(projectContext);

        Assert.Equal("GetUsers", capturedPlanService.LastFormState!.MethodName);
    }

    [Fact]
    public void AddQuery_CustomResultTypeFromPicker_EnablesBuildAndFlowsIntoFormState()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var capturedPlanService = new CapturingAddQueryPlanService();
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubEmbeddedSessionHost(),
            capturedPlanService,
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());

        session.UpdateWorkspace(projectContext);
        session.QueryName = "GetUsers";
        session.ResultTypePicker.SearchText = "ExternalResult";

        Assert.True(session.CanBuildPlan);

        session.BuildPlan(projectContext);

        Assert.Equal("ExternalResult", capturedPlanService.LastFormState!.ResultTypeName);
        Assert.False(capturedPlanService.LastFormState.CreateCustomDto);
    }

    [Fact]
    public void AddQuery_CustomResultType_CycledPrefixChangesResponseShape()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var capturedPlanService = new CapturingAddQueryPlanService();
        var session = new AddQueryRootSessionViewModel(
            new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
            new StubEmbeddedSessionHost(),
            capturedPlanService,
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());

        session.UpdateWorkspace(projectContext);
        session.QueryName = "GetUsers";
        session.ResultTypePicker.SearchText = "ExternalResult";
        session.ResultTypePicker.CyclePrefixForwardCommand.Execute(null);

        session.BuildPlan(projectContext);
        Assert.Equal(ResponseShape.List, capturedPlanService.LastFormState!.ResponseShape);

        session.ResultTypePicker.CyclePrefixForwardCommand.Execute(null);

        session.BuildPlan(projectContext);
        Assert.Equal(ResponseShape.Enumerable, capturedPlanService.LastFormState!.ResponseShape);
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
            new StubEmbeddedSessionHost(),
            new StubAddQueryPlanService(),
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());

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
            new StubEmbeddedSessionHost(),
            new StubAddQueryPlanService(),
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());
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
        var generatorHost = new GeneratorHostViewModel(workspaceStore, CreateGeneratorCatalog(), sessionService);
        var mainWindow = new MainWindowViewModel(
            new StubThemeService(),
            workspaceStore,
            sessionService,
            generatorHost);

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
    public void AddWebPage_BuildPlan_DelegatesCompositeBuildToPlanService()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateWebProjectModel(tmp.Root));
        var webPagePlan = new GenerationPlan();
        webPagePlan.AddCreateFile(Path.Combine(tmp.Root, "sentinel.razor"), "@page \"/users\"");
        var planService = new CapturingAddWebPagePlanService(webPagePlan);
        var session = new AddWebPageRootSessionViewModel(
            new GenerationActionDescriptor("add-web-page", "Add Web Page", "UI", "Ready", true),
            new StubEmbeddedSessionHost(),
            planService,
            new StubAddQueryPlanService(),
            new AddQueryScenarioOutlineBuilder(),
            new AddWebPageScenarioOutlineBuilder(),
            new QueryServiceSuggestionService());

        session.UpdateWorkspace(projectContext);
        session.Route = "/users/list";
        session.CreateImports = false;
        session.QueryPicker.Picker.SelectAllCommand.Execute(null);

        var builtPlan = session.BuildPlan(projectContext);

        Assert.Same(webPagePlan, builtPlan);
        Assert.NotNull(planService.LastFormState);
        Assert.Equal("Users", planService.LastFormState!.WebFeaturePath);
        Assert.Equal("UsersPage", planService.LastFormState.PageName);
        Assert.Equal("/users/list", planService.LastFormState.Route);
        Assert.False(planService.LastFormState.CreateImports);
        Assert.Single(planService.LastFormState.QueryBindings);
        Assert.Equal("GetUsersQuery", planService.LastFormState.QueryBindings[0].QueryName);
    }

    [Fact]
    public void AddWebPagePlanService_BuildPlan_MergesQueryDraftPlansWithPagePlan()
    {
        using var tmp = new TempProject();
        var context = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateWebProjectModel(tmp.Root));
        var queryPlanService = new CapturingAddQueryPlanServiceWithPlanFactory(draft =>
        {
            var plan = new GenerationPlan();
            plan.AddCreateFile(
                Path.Combine(tmp.Root, "Application", "Features", draft.FeaturePath ?? "Users", "Queries", $"{draft.QueryName}.cs"),
                $"public class {draft.QueryName} {{ }}");
            return plan;
        });
        var planService = new AddWebPagePlanService(new CoreWorkflowFactory(), queryPlanService);
        var formState = new AddWebPageFormState(
            "Users",
            "Users",
            "UsersPage",
            "/users",
            true,
            [new WebPageQueryBindingState("GetUsersQuery", "UserDto", string.Empty, ResponseShape.Single, true)],
            [new AddQueryFormState(
                "Users",
                "Users",
                "GetUsersQuery",
                "UserDto",
                false,
                null,
                ResponseShape.Single,
                [],
                null,
                false,
                "GetUsersAsync",
                false,
                false,
                false)]);

        var plan = planService.BuildPlan(context, formState);

        Assert.Single(queryPlanService.BuiltDrafts);
        Assert.Contains(plan.Operations, operation => operation.Path.EndsWith("GetUsersQuery.cs", StringComparison.Ordinal));
        Assert.Contains(plan.Operations, operation => operation.Path.EndsWith("UsersPage.razor", StringComparison.Ordinal));
        Assert.Contains(plan.Operations, operation => operation.Path.EndsWith("_Imports.razor", StringComparison.Ordinal));
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
            new StubEmbeddedSessionHost(),
            new StubAddQueryPlanService(),
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());

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
            new StubEmbeddedSessionHost(),
            new StubAddQueryPlanService(),
            new StubAddDtoPlanService(),
            new AddDtoScenarioOutlineBuilder(),
            new QueryServiceSuggestionService(),
            new AddQueryScenarioOutlineBuilder());

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

    private static WorkspaceGenerationCoordinator CreateCoordinator()
    {
        return new WorkspaceGenerationCoordinator(
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
            new AddQueryScenarioDefinition(host => new AddQueryRootSessionViewModel(
                new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
                host,
                new StubAddQueryPlanService(),
                new StubAddDtoPlanService(),
                new AddDtoScenarioOutlineBuilder(),
                new QueryServiceSuggestionService(),
                new AddQueryScenarioOutlineBuilder()))
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

    private sealed class CapturingAddWebPagePlanService : IAddWebPagePlanService
    {
        private readonly GenerationPlan _plan;

        public CapturingAddWebPagePlanService(GenerationPlan plan)
        {
            _plan = plan;
        }

        public AddWebPageFormState? LastFormState { get; private set; }

        public GenerationPlan BuildPlan(ProjectWorkspaceContext context, AddWebPageFormState formState)
        {
            LastFormState = formState;
            return _plan;
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

    private sealed class StubEmbeddedSessionHost : IEmbeddedSessionHost
    {
        public void Open<TDraft>(IEmbeddedGeneratorSessionViewModel<TDraft> session, Action<TDraft> onCompleted)
        {
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

    private sealed partial class TestPlanBuildingRootSession : ObservableObject, IPlanBuildingRootSessionViewModel
    {
        private readonly Func<GenerationPlan> _buildPlan;

        public TestPlanBuildingRootSession(Func<GenerationPlan> buildPlan)
        {
            _buildPlan = buildPlan;
        }

        public string SessionId => "root:plan";

        public GenerationActionDescriptor ActionDescriptor => new("plan", "Plan", "Tests", "Ready", true);

        public string DisplayName => "Plan Builder";

        public string Summary => "Builds a test plan.";

        public bool IsRoot => true;

        public bool HasUnsavedChanges => true;

        public bool CanClose => true;

        [ObservableProperty]
        private bool _canBuildPlan = true;

        public GenerationPlan BuildPlan(ProjectWorkspaceContext workspaceContext) => _buildPlan();

        public IReadOnlyList<ScenarioNodeViewModel> GetScenarioNodes() => [];
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
        Assert.All(items, i => Assert.False(i.IsRuntime));
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
    public void MultiSelectListPicker_AddRuntimeItem_DoesNotCrash()
    {
        var picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item?.ToString() ?? string.Empty,
        };

        picker.AddRuntime("IMyService", isSelected: true);

        var items = picker.FilteredItems.Cast<WrappedListItem>().ToList();
        Assert.Single(items);
        Assert.True(items[0].IsRuntime);
        Assert.True(items[0].IsSelected);
    }

    [Fact]
    public void MultiSelectListPicker_AddRuntimeItemThenToggle_DoesNotCrash()
    {
        var picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item?.ToString() ?? string.Empty,
        };

        picker.AddRuntime("IMyService", isSelected: true);
        var items = picker.FilteredItems.Cast<WrappedListItem>().ToList();

        picker.ToggleItemCommand.Execute(items[0].OriginalItem);

        var afterToggle = picker.FilteredItems.Cast<WrappedListItem>().ToList();
        Assert.False(afterToggle[0].IsSelected);
    }

    [Fact]
    public void MultiSelectListPicker_RuntimeSelectionSurvivesRebuild()
    {
        var picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item?.ToString() ?? string.Empty,
        };

        picker.AddRuntime("IMyService", isSelected: true);
        picker.SearchText = "My";
        picker.SearchText = string.Empty;

        var items = picker.FilteredItems.Cast<WrappedListItem>().ToList();
        Assert.Single(items);
        Assert.True(items[0].IsSelected);
        Assert.Contains("IMyService", picker.SelectedDisplayTexts);
    }

    [Fact]
    public void MultiSelectListPicker_SetDiscoveredThenAddRuntime_ShowsBoth()
    {
        var picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item?.ToString() ?? string.Empty,
        };

        picker.SetDiscovered(new List<string> { "ICurrentUserContext" });
        picker.AddRuntime("IMyService", isSelected: true);

        var items = picker.FilteredItems.Cast<WrappedListItem>().ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.DisplayText == "ICurrentUserContext" && !i.IsRuntime);
        Assert.Contains(items, i => i.IsRuntime);
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
            new StubEmbeddedSessionHost(),
            new AddDtoScenarioOutlineBuilder(),
            isStandalone: true);

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
    public void DtoRootSession_BuildPlan_UsesCustomSubfolderFromPickerSearchText()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var capturedPlanService = new CapturingAddDtoPlanService();
        var session = new DtoRootSessionViewModel(
            actionDescriptor: null,
            capturedPlanService,
            new StubEmbeddedSessionHost(),
            new AddDtoScenarioOutlineBuilder(),
            isStandalone: true);

        session.UpdateWorkspace(projectContext);
        session.DtoName = "UserLookupDto";
        session.SubfolderPicker.SearchText = "Billing";

        session.BuildPlan(projectContext);

        Assert.NotNull(capturedPlanService.LastFormState);
        Assert.Equal("Billing", capturedPlanService.LastFormState!.Subfolder);
    }

    [Fact]
    public void CommandRootSession_ScenarioOutline_DoesNotExposeCustomDtoStep()
    {
        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubEmbeddedSessionHost(),
            new StubAddCommandPlanService(),
            new AddCommandScenarioOutlineBuilder(),
            new StubAddRepositoryPlanService(),
            new AddRepositoryScenarioOutlineBuilder(),
            new StubAddEntityPlanService(),
            new AddEntityScenarioOutlineBuilder(),
            new EfEntityPreparationService());

        var nodes = session.GetScenarioNodes();

        Assert.DoesNotContain(nodes, node => node.Title.Contains("DTO", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nodes, node => node.Title == "Dependencies");
    }

    [Fact]
    public void CommandRootSession_CustomResultTypeFromPicker_RecomputesResultType()
    {
        using var tmp = new TempProject();
        var projectContext = new ProjectWorkspaceContext(
            tmp.Root,
            GeneratorConfig.ForTargetRoot(tmp.Root),
            CreateProjectModel(tmp.Root));
        var capturedPlanService = new CapturingAddCommandPlanService();
        var session = new CommandRootSessionViewModel(
            new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
            new StubEmbeddedSessionHost(),
            capturedPlanService,
            new AddCommandScenarioOutlineBuilder(),
            new StubAddRepositoryPlanService(),
            new AddRepositoryScenarioOutlineBuilder(),
            new StubAddEntityPlanService(),
            new AddEntityScenarioOutlineBuilder(),
            new EfEntityPreparationService());

        session.UpdateWorkspace(projectContext);
        session.CommandName = "CreateUser";
        session.HasResponseType = true;

        session.ResultTypePicker.SearchText = "ExternalResult";

        Assert.True(session.CanBuildPlan);

        session.BuildPlan(projectContext);

        Assert.Equal("ExternalResult", capturedPlanService.LastFormState!.ResponseType);
    }

    [Fact]
    public void MultiSelectListPicker_SetDiscoveredEmpty_AddRuntime_HasRuntimeItem()
    {
        var picker = new MultiSelectListPickerViewModel
        {
            ItemNameSelector = item => item?.ToString() ?? string.Empty,
            ItemKeySelector = item => item?.ToString() ?? string.Empty,
        };

        picker.SetDiscovered(new List<object>());
        picker.AddRuntime("IMyService", isSelected: true);

        var items = picker.FilteredItems.Cast<WrappedListItem>().ToList();
        Assert.Single(items);
        Assert.True(items[0].IsRuntime);
        Assert.True(items[0].CanEdit);
        Assert.True(items[0].CanRemove);
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
    public void EntityRootSession_BuildDraft_DisablesInterfaceAndDomainMethodsForGui()
    {
        var session = CreateEntitySession();
        session.EntityNameCyclic.Text = "Customer";
        session.ManualProperties[0].Type = "string";
        session.ManualProperties[0].Name = "Name";
        session.GenerateFactoryMethod = true;
        session.GenerateEfMapping = true;

        var draft = session.BuildDraft();

        Assert.False(draft.GenerateInterface);
        Assert.Empty(draft.DomainMethods);
    }

    private static EntityRootSessionViewModel CreateEntitySession()
    {
        return new EntityRootSessionViewModel(
            actionDescriptor: null,
            new StubAddEntityPlanService(),
            new AddEntityScenarioOutlineBuilder(),
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
