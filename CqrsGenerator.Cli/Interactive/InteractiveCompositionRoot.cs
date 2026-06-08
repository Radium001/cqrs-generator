using CqrsGenerator.Cli.Interactive.Wizards;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive;

public sealed class InteractiveCompositionRoot(GeneratorConfig config, IAnsiConsole console)
{
    public IReadOnlyList<InteractiveMenuAction> BuildActions()
    {
        var prompts = new PromptService(console);
        var workflows = new CoreWorkflowFactory();
        var efEntityPreparationService = new EfEntityPreparationService();
        var sections = new WizardSectionRenderer(console);
        var folderPicker = new FolderPicker(console);
        var entityWizard = new EntityWizard(prompts, sections, console, folderPicker, workflows, efEntityPreparationService);
        var repositoryWizard = new RepositoryWizard(prompts, sections, console, entityWizard, workflows);
        var commandWizard = new CommandWizard(prompts, sections, console, repositoryWizard, workflows);
        var queryServiceWizard = new QueryServiceWizard(prompts, sections, console, workflows);
        var dtoWizard = new DtoWizard(prompts, sections, folderPicker, workflows);
        var queryWizard = new QueryWizard(prompts, sections, console, dtoWizard, workflows);
        var featureWizard = new FeatureWizard(sections, console, queryWizard, commandWizard, workflows);
        var webPageWizard = new WebPageWizard(prompts, sections, console, workflows);
        var planExecutor = new InteractivePlanExecutor(console);

        return
        [
            new InteractiveMenuAction("Новая feature", model => planExecutor.Run(config, featureWizard.CreatePlan(config, model))),
            new InteractiveMenuAction("Добавить query", model => planExecutor.Run(config, queryWizard.CreatePlan(config, model))),
            new InteractiveMenuAction("Добавить command", model => planExecutor.Run(config, commandWizard.CreatePlan(config, model))),
            new InteractiveMenuAction("Добавить DTO", model => planExecutor.Run(config, dtoWizard.CreatePlan(config, model))),
            new InteractiveMenuAction("Добавить query service", model => planExecutor.Run(config, queryServiceWizard.CreatePlan(config, model))),
            new InteractiveMenuAction("Добавить repository", model => planExecutor.Run(config, repositoryWizard.CreatePlan(config, model)?.Plan)),
            new InteractiveMenuAction("Добавить domain entity", model => planExecutor.Run(config, entityWizard.CreatePlan(config, model))),
            new InteractiveMenuAction("Добавить Web-страницу/компонент", model => planExecutor.Run(config, webPageWizard.CreatePlan(config, model))),
            new InteractiveMenuAction("Посмотреть проект", RenderProject),
        ];
    }

    private void RenderProject(ProjectModel model)
    {
        var table = new Table().Title("Проект").AddColumn("Раздел").AddColumn("Найдено");
        table.AddRow("Features", model.Features.Count.ToString());
        table.AddRow("DTO", model.Dtos.Count.ToString());
        table.AddRow("Queries", model.Queries.Count.ToString());
        table.AddRow("Commands", model.Commands.Count.ToString());
        table.AddRow("Query services", model.QueryServices.Count.ToString());
        table.AddRow("Repositories", model.Repositories.Count.ToString());
        table.AddRow("Web features", model.WebFeatures.Count.ToString());
        table.AddRow("DI registrations", model.DependencyInjection.Registrations.Count.ToString());
        console.Write(table);
    }
}
