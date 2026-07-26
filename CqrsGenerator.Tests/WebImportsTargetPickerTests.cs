using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Tests;

public sealed class WebImportsTargetPickerTests
{
    [Fact]
    public void Reload_SelectsMatchingWebFeatureByDefault_AndAllowsOverride()
    {
        var picker = new WebImportsTargetPickerViewModel();
        var project = CreateProject(
            new WebFeatureInfo("Applications", "Applications", @"C:\Project\Web\Features\Applications"),
            new WebFeatureInfo("ServicesHistoryJournal", "ServicesHistoryJournal", @"C:\Project\Web\Features\ServicesHistoryJournal"));

        picker.Reload(project, "Applications", preferredRef: null);

        Assert.Equal("Applications", picker.SelectedRef?.FeaturePath);

        var servicesHistory = picker.Picker.Items
            .Cast<WebFeatureChoiceViewModel>()
            .Single(choice => choice.RelativePath == "ServicesHistoryJournal");
        picker.Picker.SelectRawItem(servicesHistory);

        Assert.Equal("ServicesHistoryJournal", picker.SelectedRef?.FeaturePath);
    }

    [Fact]
    public void Reload_WithoutMatchingFeature_DefaultsToNoUpdate()
    {
        var picker = new WebImportsTargetPickerViewModel();

        picker.Reload(
            CreateProject(new WebFeatureInfo("Applications", "Applications", @"C:\Project\Web\Features\Applications")),
            "Contracts",
            preferredRef: null);

        Assert.Null(picker.SelectedRef);
    }

    [Fact]
    public void Reload_PreservesExplicitNoUpdateSelection()
    {
        var picker = new WebImportsTargetPickerViewModel();
        var project = CreateProject(
            new WebFeatureInfo("Applications", "Applications", @"C:\Project\Web\Features\Applications"));

        picker.Reload(
            project,
            "Applications",
            preferredRef: null,
            hasExplicitSelection: true);

        Assert.Null(picker.SelectedRef);
    }

    private static ProjectModel CreateProject(params WebFeatureInfo[] webFeatures) =>
        new()
        {
            Paths = new ProjectPaths(
                @"C:\Project",
                @"C:\Project\Application",
                @"C:\Project\Application\Features",
                @"C:\Project\Infrastructure",
                @"C:\Project\Infrastructure\Data\QueryServices",
                @"C:\Project\Infrastructure\Data\Repositories",
                @"C:\Project\Infrastructure\DependencyInjection.cs",
                @"C:\Project\Web",
                @"C:\Project\Web\Features"),
            WebFeatures = webFeatures,
            DependencyInjection = new DependencyInjectionInfo(
                @"C:\Project\Infrastructure\DependencyInjection.cs",
                []),
        };
}
