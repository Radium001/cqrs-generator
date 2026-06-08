namespace CqrsGenerator.Core.Configuration;

public sealed class GeneratorConfig
{
    private GeneratorConfig(string targetRootPath, ProjectConventions conventions)
    {
        TargetRootPath = Path.GetFullPath(targetRootPath);
        Conventions = conventions;
    }

    public string TargetRootPath { get; }

    public ProjectConventions Conventions { get; }

    public string RootNamespace => Conventions.ApplicationRootNamespace;

    public string ApplicationPath => Conventions.ApplicationPath;

    public string InfrastructurePath => Conventions.InfrastructurePath;

    public string WebPath => Conventions.WebPath;

    public string FeatureRoot => Conventions.FeatureRoot;

    public string DtoFolderName => Conventions.DtoFolderName;

    public string InterfacesFolderName => Conventions.InterfacesFolderName;

    public string ModelsFolderName => Conventions.ModelsFolderName;

    public string QueriesFolderName => Conventions.QueriesFolderName;

    public string CommandsFolderName => Conventions.CommandsFolderName;

    public string ApplicationFeatureRootPath =>
        Path.GetFullPath(Path.Combine(TargetRootPath, ApplicationPath, FeatureRoot));

    public string WebFeatureRootPath =>
        Path.GetFullPath(Path.Combine(TargetRootPath, WebPath, FeatureRoot));

    public string QueryServicesPath =>
        Path.GetFullPath(Path.Combine(TargetRootPath, Conventions.QueryServicesPath));

    public string QueryServicesFolderName =>
        Path.GetFileName(Conventions.QueryServicesPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public string RepositoriesPath =>
        Path.GetFullPath(Path.Combine(TargetRootPath, Conventions.RepositoriesPath));

    public string DependencyInjectionPath =>
        Path.GetFullPath(Path.Combine(TargetRootPath, Conventions.DependencyInjectionPath));

    public static GeneratorConfig ForTargetRoot(string targetRootPath) =>
        new(targetRootPath, ProjectConventions.Default);

    public static GeneratorConfig ForTargetRoot(string targetRootPath, ProjectConventions conventions) =>
        new(targetRootPath, conventions);
}
