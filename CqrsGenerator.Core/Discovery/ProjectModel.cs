namespace CqrsGenerator.Core.Discovery;

public sealed record ProjectPaths(
    string Root,
    string Application,
    string ApplicationFeatures,
    string Infrastructure,
    string QueryServices,
    string Repositories,
    string DependencyInjection,
    string Web,
    string WebFeatures);

public sealed record FeatureInfo(string Name, string RelativePath, string Path);

public sealed record DtoSubfolderInfo(string FeaturePath, string RelativePath);

public enum DtoLocationKind
{
    SharedFeatureDto,
    LocalQueryDto,
}

public sealed record DtoInfo(
    string Name,
    string FeaturePath,
    string Path,
    string Namespace,
    string DisplayName,
    DtoLocationKind LocationKind = DtoLocationKind.SharedFeatureDto,
    string? OwnerQueryName = null);

public sealed record QueryInfo(string Name, string FeaturePath, string Path, string Namespace);

public sealed record CommandInfo(string Name, string FeaturePath, string Path, string Namespace);

public enum QueryServiceImplementationPlacement
{
    Missing,
    CanonicalFeaturePath,
    NonCanonical,
    Ambiguous,
}

public sealed record QueryServiceInfo(
    string InterfaceName,
    string FeaturePath,
    string InterfacePath,
    string? ImplementationName,
    string? ImplementationPath,
    QueryServiceImplementationPlacement ImplementationPlacement = QueryServiceImplementationPlacement.Missing,
    int ImplementationCandidateCount = 0);

public sealed record RepositoryInfo(string InterfaceName, string Path);

public sealed record WebFeatureInfo(string Name, string RelativePath, string Path);

public sealed record DiRegistrationInfo(string ServiceType, string ImplementationType, string Lifetime, string Path);

public sealed record DependencyInjectionInfo(string Path, IReadOnlyList<DiRegistrationInfo> Registrations);

public sealed record EntityInfo(string Name, string Path, string DisplayName, string Namespace, string? RelativePath);

public sealed class ProjectModel
{
    public required ProjectPaths Paths { get; init; }

    public IReadOnlyList<FeatureInfo> Features { get; init; } = [];

    public IReadOnlyList<DtoInfo> Dtos { get; init; } = [];

    public IReadOnlyList<DtoSubfolderInfo> DtoSubfolders { get; init; } = [];

    public IReadOnlyList<QueryInfo> Queries { get; init; } = [];

    public IReadOnlyList<CommandInfo> Commands { get; init; } = [];

    public IReadOnlyList<QueryServiceInfo> QueryServices { get; init; } = [];

    public IReadOnlyList<RepositoryInfo> Repositories { get; init; } = [];

    public IReadOnlyList<WebFeatureInfo> WebFeatures { get; init; } = [];

    public required DependencyInjectionInfo DependencyInjection { get; init; }

    public IReadOnlyList<EntityInfo> Entities { get; init; } = [];
}
