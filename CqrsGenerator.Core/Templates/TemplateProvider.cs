namespace CqrsGenerator.Core.Templates;

public sealed class TemplateProvider
{
    private readonly string _templateRoot;

    public TemplateProvider()
        : this(GetDefaultTemplateRoot())
    {
    }

    public TemplateProvider(string templateRoot)
    {
        _templateRoot = Path.GetFullPath(templateRoot);
    }

    public string GetTemplate(string name)
    {
        var path = Path.Combine(_templateRoot, NormalizeTemplatePath(name));
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        return TemplateCatalog.GetBuiltIn(name);
    }

    private static string NormalizeTemplatePath(string name) =>
        name.Replace('/', Path.DirectorySeparatorChar).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static string GetDefaultTemplateRoot()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Templates"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "Templates")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "CqrsGenerator.Core", "Templates")),
        };

        return candidates.FirstOrDefault(Directory.Exists)
               ?? Path.Combine(baseDirectory, "Templates");
    }
}
