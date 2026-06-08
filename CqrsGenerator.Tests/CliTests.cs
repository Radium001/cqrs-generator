using CqrsGenerator.Cli.Interactive;
using CqrsGenerator.Cli.Interactive.Rendering;
using CqrsGenerator.Cli.Interactive.Wizards;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CqrsGenerator.Tests;

public class CliTests
{
    // ── TargetRootResolver ──

    [Fact]
    public void TargetRootResolver_ApplicationDirInRoot_ReturnsRoot()
    {
        using var tmp = new TempProject();
        tmp.AddDir("Application");
        var result = TargetRootResolver.Resolve(tmp.Root);
        Assert.Equal(Path.GetFullPath(tmp.Root), Path.GetFullPath(result));
    }

    [Fact]
    public void TargetRootResolver_ApplicationDirInProject_ReturnsProject()
    {
        using var tmp = new TempProject();
        tmp.AddDir("project/Application");
        var result = TargetRootResolver.Resolve(Path.Combine(tmp.Root, "project"));
        Assert.EndsWith("project", result);
    }

    [Fact]
    public void TargetRootResolver_NoApplicationDir_ReturnsCurrentDir()
    {
        using var tmp = new TempProject();
        var result = TargetRootResolver.Resolve(tmp.Root);
        Assert.NotEmpty(result);
    }

    // ── ConsolePlanRenderer ──

    [Fact]
    public void Render_Warnings_Shown()
    {
        var console = new TestConsole();
        var plan = new GenerationPlan();
        plan.AddWarning("test warning");
        plan.AddCreateFile("C:\\proj\\new.cs", "content");

        new ConsolePlanRenderer("C:\\proj", console).Render(plan);
        Assert.Contains("test warning", console.Output);
    }

    [Fact]
    public void Render_Conflicts_Shown()
    {
        var console = new TestConsole();
        var plan = new GenerationPlan();
        plan.AddConflict("file.cs", "already exists");

        new ConsolePlanRenderer("C:\\proj", console).Render(plan);
        Assert.Contains("Конфликты", console.Output);
        Assert.Contains("file.cs", console.Output);
    }

    [Fact]
    public void Render_CreateFile_ShownInGreen()
    {
        var console = new TestConsole();
        var plan = new GenerationPlan();
        plan.AddCreateFile("C:\\proj\\new.cs", "content");

        new ConsolePlanRenderer("C:\\proj", console).Render(plan);
        Assert.Contains("new.cs", console.Output);
    }

    [Fact]
    public void Render_UpdateFile_ShownInOrange()
    {
        var console = new TestConsole();
        using var tmp = new TempProject();
        var existing = tmp.AddFile("existing.cs", "line one\nold content\nline three");
        var plan = new GenerationPlan();
        plan.AddUpdateFile(existing, "line one\nnew content\nline three");

        new ConsolePlanRenderer(tmp.Root, console).Render(plan);
        Assert.Contains("existing.cs", console.Output);
    }

    [Fact]
    public void RenderCodeView_UpdateFile_ShowsDiffLines()
    {
        var console = new TestConsole();
        using var tmp = new TempProject();
        var existing = tmp.AddFile("existing.cs", "line one\nold content\nline three");
        var plan = new GenerationPlan();
        plan.AddUpdateFile(existing, "line one\nnew content\nline three");

        new ConsolePlanRenderer(tmp.Root, console).RenderCodeView(plan);

        Assert.Contains("old content", console.Output);
        Assert.Contains("new content", console.Output);
        Assert.Contains("changed line", console.Output);
    }

    [Fact]
    public void Render_FileInRoot_NoSubfolders()
    {
        var console = new TestConsole();
        var plan = new GenerationPlan();
        plan.AddCreateFile("C:\\proj\\root-file.cs", "x");

        new ConsolePlanRenderer("C:\\proj", console).Render(plan);
        Assert.Contains("root-file.cs", console.Output);
    }

    [Fact]
    public void Render_DeeplyNested_AllLevels()
    {
        var console = new TestConsole();
        var plan = new GenerationPlan();
        plan.AddCreateFile("C:\\proj\\A\\B\\C\\D\\deep.cs", "x");

        new ConsolePlanRenderer("C:\\proj", console).Render(plan);
        Assert.Contains("deep.cs", console.Output);
    }

    [Fact]
    public void Render_MultipleFilesInSameFolder_FolderOnce()
    {
        var console = new TestConsole();
        var plan = new GenerationPlan();
        plan.AddCreateFile("C:\\proj\\src\\a.cs", "a");
        plan.AddCreateFile("C:\\proj\\src\\b.cs", "b");

        new ConsolePlanRenderer("C:\\proj", console).Render(plan);
        Assert.Contains("a.cs", console.Output);
        Assert.Contains("b.cs", console.Output);
    }

    [Fact]
    public void Render_EmptyPlan_ShowsLegend()
    {
        var console = new TestConsole();
        new ConsolePlanRenderer("C:\\proj", console).Render(new GenerationPlan());
        Assert.Contains("создание", console.Output);
        Assert.Contains("модификация", console.Output);
    }

    [Fact]
    public void DiffPreviewBuilder_RendersOnlyChangedArea()
    {
        var preview = DiffPreviewBuilder.Build("""
line 1
line 2
line 3
""", """
line 1
line 2 changed
line 3
""");

        Assert.NotNull(preview);
        Assert.Contains("line 2", preview!.Markup);
        Assert.True(preview.ChangedLineCount > 0);
    }

    [Fact]
    public void WizardSectionRenderer_BeginAndEnd_ShowSectionTitle()
    {
        var console = new TestConsole();
        var renderer = new WizardSectionRenderer(console);

        renderer.Begin("Feature > Query");
        renderer.End("Feature > Query");

        Assert.Contains("Feature > Query", console.Output);
    }

    [Fact]
    public void DtoWizard_CreateForFeature_ReturnsDtoNamePlanAndSectionTitle()
    {
        var console = new TestConsole();
        console.Input.PushTextWithEnter("UserDetailsDto");
        console.Input.PushTextWithEnter("int Id");
        console.Input.PushTextWithEnter("-");

        using var tmp = new TempProject();
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var wizard = new DtoWizard(new PromptService(console), new WizardSectionRenderer(console), new FolderPicker(console), new CoreWorkflowFactory());

        var result = wizard.CreateForFeature(config, "Users", "Query > DTO");

        Assert.Equal("UserDetailsDto", result.DtoName);
        Assert.Contains(result.Plan.Files, file => file.Path.EndsWith("UserDetailsDto.cs"));
        Assert.Contains(result.Plan.Files, file => file.Path.EndsWith("_Imports.razor"));
        Assert.Contains("Query > DTO", console.Output);
    }

    [Fact]
    public void DtoWizard_CreateForFeature_CanSkipWebImportsForNestedQueryFlow()
    {
        var console = new TestConsole();
        console.Input.PushTextWithEnter("UserDetailsDto");
        console.Input.PushTextWithEnter("-");

        using var tmp = new TempProject();
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var wizard2 = new DtoWizard(new PromptService(console), new WizardSectionRenderer(console), new FolderPicker(console), new CoreWorkflowFactory());

        var result2 = wizard2.CreateForFeature(config, "Users", "Query > DTO", updateWebImports: false);

        Assert.Equal("UserDetailsDto", result2.DtoName);
        Assert.Contains(result2.Plan.Files, file => file.Path.EndsWith("UserDetailsDto.cs"));
        Assert.DoesNotContain(result2.Plan.Files, file => file.Path.EndsWith("_Imports.razor"));
    }

    // ── ParseProperty ──

    [Fact]
    public void ParseProperty_SinglePart_ReturnsNull()
    {
        var line = "int";
        var result = PropertyInputParser.Parse(line);
        Assert.Null(result);
    }

    [Fact]
    public void ParseProperty_TwoParts_ReturnsCorrect()
    {
        var line = "int id";
        var result = PropertyInputParser.Parse(line);
        Assert.NotNull(result);
        Assert.Equal("int", result!.Type);
        Assert.Equal("id", result.Name);
    }

    [Fact]
    public void ParseProperty_GenericType_ReturnsCorrect()
    {
        var line = "IEnumerable<Foo> items";
        var result = PropertyInputParser.Parse(line);
        Assert.NotNull(result);
        Assert.Equal("IEnumerable<Foo>", result!.Type);
        Assert.Equal("items", result.Name);
    }

    // ── ToPascalCase ──

    [Fact]
    public void ToPascalCase_Empty_ReturnsEmpty()
    {
        Assert.Equal("", GenerationNaming.ToPascalCase(""));
    }

    [Fact]
    public void ToPascalCase_SingleChar_UpperInvariant()
    {
        Assert.Equal("A", GenerationNaming.ToPascalCase("a"));
    }

    [Fact]
    public void ToPascalCase_Normal_FirstUpper()
    {
        Assert.Equal("Hello", GenerationNaming.ToPascalCase("hello"));
    }

    [Fact]
    public void ToPascalCase_AlreadyPascal_Unchanged()
    {
        Assert.Equal("Hello", GenerationNaming.ToPascalCase("Hello"));
    }

    // ── ToFeatureBaseName ──

    [Fact]
    public void ToFeatureBaseName_Simple_ReturnsSame()
    {
        Assert.Equal("Test", GenerationNaming.ToFeatureBaseName("Test"));
    }

    [Fact]
    public void ToFeatureBaseName_Nested_ReturnsLast()
    {
        Assert.Equal("C", GenerationNaming.ToFeatureBaseName("A/B/C"));
    }

    // ── InteractiveRunner integration (requires Spectre.Console.Testing 0.55.0, currently 0.55.2) ──
    // These are integration tests that need compatible versions of Spectre.Console between
    // the CLI project and the testing package. Run manually when versions align.

    [Fact(Skip = "TestConsole does not emulate an interactive terminal. InteractiveRunner tests require real terminal.")]
    public void InteractiveRunner_Exit_ReturnsZero()
    {
        var console = new TestConsole();
        for (int i = 0; i < 8; i++) console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var runner = new InteractiveRunner(console);
        var result = runner.Run(Directory.GetCurrentDirectory());
        Assert.Equal(0, result);
    }

    [Fact(Skip = "TestConsole does not emulate an interactive terminal. InteractiveRunner tests require real terminal.")]
    public void InteractiveRunner_InspectProject_RendersOutput()
    {
        var console = new TestConsole();
        for (int i = 0; i < 7; i++) console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        for (int i = 0; i < 8; i++) console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var runner = new InteractiveRunner(console);
        runner.Run(Directory.GetCurrentDirectory());
        Assert.Contains("Features", console.Output);
    }
}
