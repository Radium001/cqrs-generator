using CqrsGenerator.Core.Templates;

namespace CqrsGenerator.Tests;

public class TemplatesTests
{
    // ── TemplateCatalog ──

    [Fact]
    public void GetBuiltIn_Query_ReturnsTemplateWithIQuery()
    {
        var result = TemplateCatalog.GetBuiltIn(TemplateNames.Query);
        Assert.Contains("IQuery<", result);
        Assert.Contains("using Application.Common.Interfaces;", result);
    }

    [Fact]
    public void GetBuiltIn_Handler_ReturnsTemplateWithIRequestHandler()
    {
        var result = TemplateCatalog.GetBuiltIn(TemplateNames.Handler);
        Assert.Contains("IRequestHandler<", result);
        Assert.Contains("using MediatR;", result);
    }

    [Fact]
    public void GetBuiltIn_Dto_ReturnsTemplateWithPublicClass()
    {
        var result = TemplateCatalog.GetBuiltIn(TemplateNames.Dto);
        Assert.Contains("public class ", result);
    }

    [Fact]
    public void GetBuiltIn_UnknownName_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TemplateCatalog.GetBuiltIn("unknown_template"));
    }

    // ── TemplateProvider ──

    [Fact]
    public void TemplateProvider_ExplicitRoot_UsesThatRoot()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.sbn"), "content");

        // TemplateProvider with explicit root doesn't fall back — it looks for 
        // the file in the given root and if not found, falls back to built-in.
        // Test: find an existing built-in template via the explicit provider.
        var provider = new TemplateProvider(tmp.Path);
        var result = provider.GetTemplate(TemplateNames.Query);
        Assert.Contains("IQuery<", result);
    }

    [Fact]
    public void TemplateProvider_FileExists_ReturnsFileContent()
    {
        using var tmp = new TempDir();
        var tplPath = Path.Combine(tmp.Path, "Application", "Features", "{{ feature_path }}", "Queries", "{{ operation_name }}", "{{ query_type }}.cs.sbn");
        Directory.CreateDirectory(Path.GetDirectoryName(tplPath)!);
        File.WriteAllText(tplPath, "content from file");

        var provider = new TemplateProvider(tmp.Path);
        var result = provider.GetTemplate(TemplateNames.Query);
        Assert.Equal("content from file", result);
    }

    [Fact]
    public void TemplateProvider_FileMissing_FallsBackToBuiltIn()
    {
        using var tmp = new TempDir();
        var provider = new TemplateProvider(tmp.Path);
        var result = provider.GetTemplate(TemplateNames.Dto);
        Assert.Contains("public class ", result);
    }

    [Fact]
    public void TemplateProvider_ParameterlessConstructor_ResolvesRoot()
    {
        var provider = new TemplateProvider();
        var result = provider.GetTemplate(TemplateNames.Query);
        Assert.Contains("IQuery<", result);
    }

    // ── ScribanTemplateRenderer ──

    [Fact]
    public void ScribanTemplateRenderer_Render_SimpleModel_Works()
    {
        using var tmp = new TempDir();
        var tplPath = Path.Combine(tmp.Path, "test.sbn");
        File.WriteAllText(tplPath, "Hello {{ name }}!");

        var renderer = new ScribanTemplateRenderer(new TemplateProvider(tmp.Path));
        var result = renderer.Render("test.sbn", new { name = "World" });
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void ScribanTemplateRenderer_Render_TemplateWithErrors_Throws()
    {
        using var tmp = new TempDir();
        var tplPath = Path.Combine(tmp.Path, "test.sbn");
        File.WriteAllText(tplPath, "{{ invalid stuff @@@ }}");

        var renderer = new ScribanTemplateRenderer(new TemplateProvider(tmp.Path));
        Assert.Throws<InvalidOperationException>(() =>
            renderer.Render("test.sbn", new { }));
    }

    [Fact]
    public void ScribanTemplateRenderer_Render_NormalizesLineEndings()
    {
        using var tmp = new TempDir();
        var tplPath = Path.Combine(tmp.Path, "test.sbn");
        File.WriteAllText(tplPath, "Hello\r\n{{ name }}\r\nWorld");

        var renderer = new ScribanTemplateRenderer(new TemplateProvider(tmp.Path));
        var result = renderer.Render("test.sbn", new { name = "X" });
        Assert.DoesNotContain("\r\n", result);
        Assert.Equal("Hello\nX\nWorld", result);
    }

    [Fact]
    public void ScribanTemplateRenderer_Render_ComplexModel_RendersCorrectly()
    {
        using var tmp = new TempDir();
        var tplPath = Path.Combine(tmp.Path, "test.sbn");
        File.WriteAllText(tplPath, """
namespace {{ namespace }}
{
    public class {{ query_type }}
    {
        public int Id { get; set; }
    }
}
""");

        var renderer = new ScribanTemplateRenderer(new TemplateProvider(tmp.Path));
        var result = renderer.Render("test.sbn", new
        {
            @namespace = "App.Features.Test",
            query_type = "GetUsersQuery",
        });

        Assert.Contains("namespace App.Features.Test", result);
        Assert.Contains("class GetUsersQuery", result);
    }
}

public sealed class TempDir : IDisposable
{
    public string Path { get; }
    public TempDir() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cqrs-gen-tpl-{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); }
    public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
}
