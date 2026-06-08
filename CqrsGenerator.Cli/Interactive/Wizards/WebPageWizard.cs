using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class WebPageWizard(
    PromptService prompts,
    WizardSectionRenderer sections,
    IAnsiConsole console,
    CoreWorkflowFactory workflows)
{
    public GenerationPlan? CreatePlan(GeneratorConfig config, ProjectModel model, string sectionTitle = "Web Page Generator")
    {
        sections.Begin(sectionTitle);
        try
        {
            const string createNew = "+ Создать новую Web feature";
            const string cancel = "✕ Отмена";
            var selectedFeature = console.PromptSelection("Для какой feature создать страницу?",
                new SelectionPrompt<string>()
                    .Title("Выберите Web feature")
                    .EnableSearch()
                    .AddChoices(model.WebFeatures.Select(feature => feature.RelativePath).Concat([createNew, cancel])));

            if (selectedFeature == cancel)
            {
                return null;
            }

            var webFeaturePath = selectedFeature == createNew
                ? console.Ask<string>("Как назвать Web feature?")
                : selectedFeature;

            var pageName = prompts.AskIdentifier("Как назвать страницу?", $"{webFeaturePath.Split('/').Last()}Page");
            var defaultRoute = $"/{StringUtilities.ToKebabCase(webFeaturePath.Split('/').Last())}";
            var route = console.Prompt(new TextPrompt<string>("Какой route?").DefaultValue(defaultRoute));

            return workflows.AddWebPage(config)
                .CreatePlan(new WebPageGenerationRequest
                {
                    WebFeaturePath = webFeaturePath,
                    PageName = pageName,
                    Route = route,
                    CreateImports = true,
                });
        }
        finally
        {
            sections.End(sectionTitle);
        }
    }
}
