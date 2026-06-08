using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Tests;

public class TempProject : IDisposable
{
    public string Root { get; }

    public TempProject()
    {
        Root = Path.Combine(Path.GetTempPath(), $"cqrs-gen-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    public string AddDir(string relativePath)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string AddFile(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        return path;
    }

    public ProjectDiscovery CreateDiscovery()
    {
        var config = GeneratorConfig.ForTargetRoot(Root);
        return new ProjectDiscovery(config);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}
