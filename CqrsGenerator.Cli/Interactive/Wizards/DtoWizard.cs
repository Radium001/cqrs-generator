using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Workflows;
using Spectre.Console;

namespace CqrsGenerator.Cli.Interactive.Wizards;

public sealed class DtoWizard(
    PromptService prompts,
    WizardSectionRenderer sections,
    FolderPicker folderPicker,
    CoreWorkflowFactory workflows)
{
    public GenerationPlan? CreatePlan(GeneratorConfig config, ProjectModel model)
    {
        const string title = "DTO Generator";
        sections.Begin(title);
        try
        {
            var featurePath = prompts.AskFeature(model);
            if (featurePath is null)
            {
                return null;
            }

            return CreateDtoForFeature(config, featurePath).Plan;
        }
        finally
        {
            sections.End(title);
        }
    }

    public DtoWizardResult CreateForFeature(
        GeneratorConfig config,
        string featurePath,
        string sectionTitle = "DTO Generator",
        bool updateWebImports = true)
    {
        sections.Begin(sectionTitle);
        try
        {
            return CreateDtoForFeature(config, featurePath, updateWebImports);
        }
        finally
        {
            sections.End(sectionTitle);
        }
    }

    private DtoWizardResult CreateDtoForFeature(
        GeneratorConfig config,
        string featurePath,
        bool updateWebImports = true)
    {
        var dtoName = prompts.AskIdentifier("Как назвать DTO?", "UserDetailsDto");
        var properties = prompts.AskProperties("Введите свойства DTO по одному на строку: <тип> <Имя>. '-' или пустая строка завершает ввод.", normalizeName: false);

        var featureRoot = Path.Combine(config.ApplicationFeatureRootPath, featurePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));
        var dtoRoot = Path.Combine(featureRoot, config.DtoFolderName);

        string? subfolder = null;
        if (Directory.Exists(dtoRoot))
        {
            var choice = folderPicker.Pick(dtoRoot, "Подпапка в DTOs/?", "Без папки");
            if (choice.CreateNew)
            {
                subfolder = folderPicker.AskNewFolderName("Название подпапки для DTO", "Models");
            }
            else
            {
                subfolder = choice.RelativePath;
            }
        }

        return new DtoWizardResult(dtoName, workflows.AddDto(config)
            .CreatePlan(new AddDtoWorkflowRequest(featurePath, dtoName, properties, updateWebImports, subfolder)));
    }
}

public sealed record DtoWizardResult(string DtoName, GenerationPlan Plan);
