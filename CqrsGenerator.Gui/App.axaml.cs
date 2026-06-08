using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Services;
using CqrsGenerator.Gui.Services.Generators;
using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Gui.ViewModels.Generators;
using CqrsGenerator.Gui.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsGenerator.Gui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            var services = new ServiceCollection();
            ConfigureServices(services, mainWindow);

            var serviceProvider = services.BuildServiceProvider();
            mainWindow.DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services, MainWindow mainWindow)
    {
        services.AddSingleton<IThemeService>(_ => new ThemeService(this));
        services.AddSingleton<IWorkspaceStore, WorkspaceStore>();
        services.AddSingleton<CoreWorkflowFactory>();
        services.AddSingleton<EfEntityPreparationService>();
        services.AddSingleton<PlanPreparationService>();
        services.AddSingleton<StrictPlanApplier>();

        services.AddSingleton<IProjectScanService, ProjectScanService>();
        services.AddSingleton<IArchitectureWarningService, ArchitectureWarningService>();
        services.AddSingleton<IPlanPreviewService, PlanPreviewService>();
        services.AddSingleton<IDiffService, DiffService>();
        services.AddSingleton<IAddQueryRequestBuilder, AddQueryRequestBuilder>();
        services.AddSingleton<IAddQueryScenarioOutlineBuilder, AddQueryScenarioOutlineBuilder>();
        services.AddSingleton<IAddCommandScenarioOutlineBuilder, AddCommandScenarioOutlineBuilder>();
        services.AddSingleton<IAddDtoScenarioOutlineBuilder, AddDtoScenarioOutlineBuilder>();
        services.AddSingleton<IAddEntityScenarioOutlineBuilder, AddEntityScenarioOutlineBuilder>();
        services.AddSingleton<IAddRepositoryScenarioOutlineBuilder, AddRepositoryScenarioOutlineBuilder>();
        services.AddSingleton<IAddWebPageScenarioOutlineBuilder, AddWebPageScenarioOutlineBuilder>();
        services.AddSingleton<ICreateFeatureScenarioOutlineBuilder, CreateFeatureScenarioOutlineBuilder>();
        services.AddSingleton<ICreateFeaturePlanService, CreateFeaturePlanService>();
        services.AddSingleton<IQueryServiceSuggestionService, QueryServiceSuggestionService>();
        services.AddSingleton<IAddQueryPlanService, AddQueryPlanService>();
        services.AddSingleton<IAddDtoRequestBuilder, AddDtoRequestBuilder>();
        services.AddSingleton<IAddDtoPlanService, AddDtoPlanService>();
        services.AddSingleton<IAddEntityRequestBuilder, AddEntityRequestBuilder>();
        services.AddSingleton<IAddEntityPlanService, AddEntityPlanService>();
        services.AddSingleton<IAddRepositoryRequestBuilder, AddRepositoryRequestBuilder>();
        services.AddSingleton<IAddRepositoryPlanService, AddRepositoryPlanService>();
        services.AddSingleton<IAddCommandRequestBuilder, AddCommandRequestBuilder>();
        services.AddSingleton<IAddCommandPlanService, AddCommandPlanService>();
        services.AddSingleton<IAddWebPagePlanService, AddWebPagePlanService>();

        services.AddSingleton<IProjectOpenService>(_ => new ProjectOpenService(mainWindow));
        services.AddSingleton<IDialogService>(_ => new DialogService(mainWindow));
        services.AddSingleton<IWorkspaceShellService, WorkspaceShellService>();
        services.AddSingleton<IWorkspaceGenerationCoordinator, WorkspaceGenerationCoordinator>();
        services.AddSingleton<IWorkspaceApplyService, WorkspaceApplyService>();
        services.AddSingleton<IWorkspaceSessionService, WorkspaceSessionService>();

        services.AddSingleton<IGeneratorScenarioDefinition>(provider =>
            new AddQueryScenarioDefinition(host => new AddQueryRootSessionViewModel(
                new GenerationActionDescriptor("add-query", "Add Query", "Application", "Ready", true),
                host,
                provider.GetRequiredService<IAddQueryPlanService>(),
                provider.GetRequiredService<IAddDtoPlanService>(),
                provider.GetRequiredService<IAddDtoScenarioOutlineBuilder>(),
                provider.GetRequiredService<IQueryServiceSuggestionService>(),
                provider.GetRequiredService<IAddQueryScenarioOutlineBuilder>(),
                createFeaturePlanService: provider.GetRequiredService<ICreateFeaturePlanService>(),
                createFeatureScenarioOutlineBuilder: provider.GetRequiredService<ICreateFeatureScenarioOutlineBuilder>())));

        services.AddSingleton<IGeneratorScenarioDefinition>(provider =>
            new AddWebPageScenarioDefinition(host => new AddWebPageRootSessionViewModel(
                new GenerationActionDescriptor("add-web-page", "Add Web Page", "UI", "Ready", true),
                host,
                provider.GetRequiredService<IAddWebPagePlanService>(),
                provider.GetRequiredService<IAddQueryPlanService>(),
                provider.GetRequiredService<IAddQueryScenarioOutlineBuilder>(),
                provider.GetRequiredService<IAddWebPageScenarioOutlineBuilder>(),
                provider.GetRequiredService<IQueryServiceSuggestionService>())));

        services.AddSingleton<IGeneratorScenarioDefinition>(provider =>
            new AddDtoScenarioDefinition(host => new DtoRootSessionViewModel(
                new GenerationActionDescriptor("add-dto", "Add DTO", "Application", "Ready", true),
                provider.GetRequiredService<IAddDtoPlanService>(),
                host,
                provider.GetRequiredService<IAddDtoScenarioOutlineBuilder>(),
                isStandalone: true)));

        services.AddSingleton<IGeneratorScenarioDefinition>(provider =>
            new AddCommandScenarioDefinition(host => new CommandRootSessionViewModel(
                new GenerationActionDescriptor("add-command", "Add Command", "Application", "Ready", true),
                host,
                provider.GetRequiredService<IAddCommandPlanService>(),
                provider.GetRequiredService<IAddCommandScenarioOutlineBuilder>(),
                provider.GetRequiredService<IAddRepositoryPlanService>(),
                provider.GetRequiredService<IAddRepositoryScenarioOutlineBuilder>(),
                provider.GetRequiredService<IAddEntityPlanService>(),
                provider.GetRequiredService<IAddEntityScenarioOutlineBuilder>(),
                provider.GetRequiredService<EfEntityPreparationService>())));

        services.AddSingleton<IGeneratorScenarioDefinition>(provider =>
            new AddRepositoryScenarioDefinition(host => new RepositoryRootSessionViewModel(
                new GenerationActionDescriptor("add-repository", "Add Repository", "Application", "Ready", true),
                provider.GetRequiredService<IAddRepositoryPlanService>(),
                host,
                provider.GetRequiredService<IAddRepositoryScenarioOutlineBuilder>(),
                provider.GetRequiredService<IAddEntityPlanService>(),
                provider.GetRequiredService<IAddEntityScenarioOutlineBuilder>(),
                provider.GetRequiredService<EfEntityPreparationService>(),
                isStandalone: true)));

        services.AddSingleton<IGeneratorScenarioDefinition>(provider =>
            new AddEntityScenarioDefinition(_ => new EntityRootSessionViewModel(
                new GenerationActionDescriptor("add-entity", "Add Entity", "Domain", "Ready", true),
                provider.GetRequiredService<IAddEntityPlanService>(),
                provider.GetRequiredService<IAddEntityScenarioOutlineBuilder>(),
                provider.GetRequiredService<EfEntityPreparationService>(),
                isStandalone: true)));

        services.AddSingleton<IGeneratorScenarioDefinition>(provider =>
            new CreateFeatureScenarioDefinition(() => new CreateFeatureRootSessionViewModel(
                new GenerationActionDescriptor("new-feature", "New Feature", "Application", "Ready", true),
                provider.GetRequiredService<ICreateFeaturePlanService>(),
                provider.GetRequiredService<ICreateFeatureScenarioOutlineBuilder>(),
                isStandalone: true)));

        services.AddSingleton<IGeneratorCatalog, GeneratorCatalog>();

        services.AddTransient<GeneratorHostViewModel>();
        services.AddTransient<MainWindowViewModel>();
    }
}
