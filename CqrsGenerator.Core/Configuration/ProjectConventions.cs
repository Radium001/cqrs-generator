namespace CqrsGenerator.Core.Configuration;

public sealed class ProjectConventions
{
    public string ApplicationRootNamespace { get; init; } = "Application";

    public string InfrastructureRootNamespace { get; init; } = "Infrastructure";

    public string WebRootNamespace { get; init; } = "Web";

    public string ApplicationPath { get; init; } = "Application";

    public string InfrastructurePath { get; init; } = "Infrastructure";

    public string WebPath { get; init; } = "Web";

    public string FeatureRoot { get; init; } = "Features";

    public string DtoFolderName { get; init; } = "DTOs";

    public string InterfacesFolderName { get; init; } = "Interfaces";

    public string ModelsFolderName { get; init; } = "Models";

    public string QueriesFolderName { get; init; } = "Queries";

    public string CommandsFolderName { get; init; } = "Commands";

    public string QueryServicesPath { get; init; } = Path.Combine("Infrastructure", "Data", "QueryServices");

    public string RepositoriesPath { get; init; } = Path.Combine("Infrastructure", "Data", "Repositories");

    public string DependencyInjectionPath { get; init; } = Path.Combine("Infrastructure", "DependencyInjection.cs");

    public static ProjectConventions Default { get; } = new();
}
