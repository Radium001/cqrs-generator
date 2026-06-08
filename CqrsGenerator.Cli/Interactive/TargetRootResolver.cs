namespace CqrsGenerator.Cli.Interactive;

public static class TargetRootResolver
{
    public static string Resolve(string currentDirectory)
    {
        var fullPath = Path.GetFullPath(currentDirectory);
        if (Directory.Exists(Path.Combine(fullPath, "Application")))
        {
            return fullPath;
        }

        var nestedProject = Path.Combine(fullPath, "project");
        if (Directory.Exists(Path.Combine(nestedProject, "Application")))
        {
            return nestedProject;
        }

        return fullPath;
    }
}
