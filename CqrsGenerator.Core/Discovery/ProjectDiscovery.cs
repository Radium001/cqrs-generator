using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;
using System.Text.RegularExpressions;

namespace CqrsGenerator.Core.Discovery;

public sealed class ProjectDiscovery(GeneratorConfig config)
{
    private static readonly Regex NamespaceRegex = new(@"^\s*namespace\s+([A-Za-z_][\w.]*)", RegexOptions.Multiline);
    private static readonly Regex QueryClassRegex = new(@"\bclass\s+([A-Za-z_]\w*)\s*:\s*IQuery\s*<", RegexOptions.Multiline);
    private static readonly Regex CommandClassRegex = new(@"\bclass\s+([A-Za-z_]\w*)\s*:\s*ICommand(?:\s*<|\b)", RegexOptions.Multiline);
    private static readonly Regex InterfaceRegex = new(@"\binterface\s+(I[A-Za-z_]\w*)", RegexOptions.Multiline);
    private static readonly Regex RepositoryInterfaceRegex = new(@"\binterface\s+(I[A-Za-z_]\w*Repository)\b", RegexOptions.Multiline);
    private static readonly Regex ClassRegex = new(@"\bclass\s+([A-Za-z_]\w*)", RegexOptions.Multiline);
    private static readonly Regex DiRegistrationRegex = new(
        @"Add(?<lifetime>Scoped|Transient|Singleton)\s*<\s*(?<service>[A-Za-z_]\w*)\s*,\s*(?<implementation>[A-Za-z_]\w*)\s*>",
        RegexOptions.Multiline);

    private static readonly string[] IgnoredDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".idea",
        "artifacts",
    ];

    public ProjectModel Discover()
    {
        var features = GetFeatureInfos();
        var dtos = features.SelectMany(GetDtoInfos).OrderBy(dto => dto.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var dtoSubfolders = features.SelectMany(GetDtoSubfolderInfos).OrderBy(dto => dto.FeaturePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dto => dto.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var queries = features.SelectMany(GetQueryInfos).OrderBy(query => query.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var commands = features.SelectMany(GetCommandInfos).OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var queryServices = features.SelectMany(GetQueryServiceInfos).OrderBy(service => service.InterfaceName, StringComparer.OrdinalIgnoreCase).ToArray();
        var repositories = GetRepositoryInfos();
        var webFeatures = GetWebFeatureInfos();
        var dependencyInjection = GetDependencyInjectionInfo();
        var entities = GetEntityInfos();

        return new ProjectModel
        {
            Paths = new ProjectPaths(
                config.TargetRootPath,
                Path.Combine(config.TargetRootPath, config.ApplicationPath),
                config.ApplicationFeatureRootPath,
                Path.Combine(config.TargetRootPath, config.InfrastructurePath),
                config.QueryServicesPath,
                config.RepositoriesPath,
                config.DependencyInjectionPath,
                Path.Combine(config.TargetRootPath, config.WebPath),
                config.WebFeatureRootPath),
            Features = features,
            Dtos = dtos,
            DtoSubfolders = dtoSubfolders,
            Queries = queries,
            Commands = commands,
            QueryServices = queryServices,
            Repositories = repositories,
            WebFeatures = webFeatures,
            DependencyInjection = dependencyInjection,
            Entities = entities,
        };
    }

    public IReadOnlyList<string> GetFeaturePaths()
    {
        return GetFeatureInfos().Select(feature => feature.RelativePath).ToArray();
    }

    public IReadOnlyList<TypeCandidate> GetDtos(string featurePath)
    {
        var dtoRoot = Path.Combine(GetFeatureRoot(featurePath), config.DtoFolderName);
        return GetTypesFromDirectory(dtoRoot);
    }

    public IReadOnlyList<TypeCandidate> GetServiceInterfaces(string featurePath)
    {
        var interfaceRoot = Path.Combine(GetFeatureRoot(featurePath), config.InterfacesFolderName);
        return GetTypesFromDirectory(interfaceRoot)
            .Where(type => type.Name.StartsWith("I", StringComparison.Ordinal) && type.Name.Length > 1)
            .ToArray();
    }

    private IReadOnlyList<FeatureInfo> GetFeatureInfos()
    {
        var root = config.ApplicationFeatureRootPath;
        if (!Directory.Exists(root))
        {
            return [];
        }

        var results = new List<FeatureInfo>();
        CollectFeatureDirectories(root, root, results);
        return results
            .OrderBy(feature => feature.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void CollectFeatureDirectories(string root, string currentDir, List<FeatureInfo> results)
    {
        foreach (var dir in Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (IsIgnored(dir))
            {
                continue;
            }

            if (IsFeatureDirectory(dir))
            {
                results.Add(new FeatureInfo(
                    Path.GetFileName(dir),
                    NormalizeRelativePath(Path.GetRelativePath(root, dir)),
                    dir));
            }
            else
            {
                CollectFeatureDirectories(root, dir, results);
            }
        }
    }

    private IEnumerable<DtoInfo> GetDtoInfos(FeatureInfo feature)
    {
        var dtoRoot = Path.Combine(feature.Path, config.DtoFolderName);
        var sharedDtos = GetTypesFromDirectory(dtoRoot)
            .Where(type => type.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase))
            .Select(type =>
            {
                var subPath = NormalizeRelativePath(Path.GetRelativePath(dtoRoot, Path.GetDirectoryName(type.Path) ?? ""));
                var displayName = subPath == "." ? type.Name : $"{subPath}/{type.Name}";
                return new DtoInfo(
                    type.Name,
                    feature.RelativePath,
                    type.Path,
                    GetNamespace(type.Path),
                    displayName,
                    DtoLocationKind.SharedFeatureDto);
            });

        return sharedDtos.Concat(GetLocalQueryDtoInfos(feature));
    }

    private IEnumerable<DtoInfo> GetLocalQueryDtoInfos(FeatureInfo feature)
    {
        var queryRoot = Path.Combine(feature.Path, config.QueriesFolderName);
        if (!Directory.Exists(queryRoot))
        {
            yield break;
        }

        foreach (var queryDirectory in Directory.EnumerateDirectories(queryRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (IsIgnored(queryDirectory))
            {
                continue;
            }

            var ownerQueryName = Path.GetFileName(queryDirectory);
            foreach (var file in Directory.EnumerateFiles(queryDirectory, "*Dto.cs", SearchOption.TopDirectoryOnly))
            {
                if (IsIgnored(file))
                {
                    continue;
                }

                yield return new DtoInfo(
                    Path.GetFileNameWithoutExtension(file),
                    feature.RelativePath,
                    file,
                    GetNamespace(file),
                    $"{ownerQueryName}/{Path.GetFileNameWithoutExtension(file)}",
                    DtoLocationKind.LocalQueryDto,
                    ownerQueryName);
            }
        }
    }

    private IEnumerable<DtoSubfolderInfo> GetDtoSubfolderInfos(FeatureInfo feature)
    {
        var dtoRoot = Path.Combine(feature.Path, config.DtoFolderName);
        if (!Directory.Exists(dtoRoot))
        {
            yield break;
        }

        foreach (var dir in Directory.EnumerateDirectories(dtoRoot, "*", SearchOption.AllDirectories))
        {
            if (IsIgnored(dir))
            {
                continue;
            }

            var relativePath = NormalizeRelativePath(Path.GetRelativePath(dtoRoot, dir));
            if (relativePath == ".")
            {
                continue;
            }

            yield return new DtoSubfolderInfo(feature.RelativePath, relativePath);
        }
    }

    private IEnumerable<QueryInfo> GetQueryInfos(FeatureInfo feature)
    {
        var queryRoot = Path.Combine(feature.Path, config.QueriesFolderName);
        return GetMatchingTypes(queryRoot, QueryClassRegex)
            .Select(type => new QueryInfo(type.Name, feature.RelativePath, type.Path, GetNamespace(type.Path)));
    }

    private IEnumerable<CommandInfo> GetCommandInfos(FeatureInfo feature)
    {
        var commandRoot = Path.Combine(feature.Path, config.CommandsFolderName);
        return GetMatchingTypes(commandRoot, CommandClassRegex)
            .Select(type => new CommandInfo(type.Name, feature.RelativePath, type.Path, GetNamespace(type.Path)));
    }

    private IEnumerable<QueryServiceInfo> GetQueryServiceInfos(FeatureInfo feature)
    {
        var interfaceRoot = Path.Combine(feature.Path, config.InterfacesFolderName);
        foreach (var type in GetMatchingTypes(interfaceRoot, InterfaceRegex)
                     .Where(type => type.Name.EndsWith("QueryService", StringComparison.Ordinal)))
        {
            var implementationName = GenerationNaming.ToDependencyName(type.Name);
            var implementation = FindImplementation(feature.RelativePath, implementationName);
            yield return new QueryServiceInfo(
                type.Name,
                feature.RelativePath,
                type.Path,
                implementation.Path is null ? null : implementationName,
                implementation.Path,
                implementation.Placement,
                implementation.CandidateCount);
        }
    }

    private IReadOnlyList<RepositoryInfo> GetRepositoryInfos()
    {
        var repositoryInterfaceRoot = Path.Combine(
            config.TargetRootPath,
            config.ApplicationPath,
            "Common",
            "Interfaces",
            "Repositories");

        return GetMatchingTypes(repositoryInterfaceRoot, RepositoryInterfaceRegex)
            .Select(type => new RepositoryInfo(type.Name, type.Path))
            .OrderBy(repository => repository.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<EntityInfo> GetEntityInfos()
    {
        var entityRoot = Path.Combine(config.TargetRootPath, "Domain", "Entities");
        if (!Directory.Exists(entityRoot))
        {
            return [];
        }

        var results = new List<EntityInfo>();

        foreach (var file in Directory.EnumerateFiles(entityRoot, "*.cs", SearchOption.TopDirectoryOnly))
        {
                var match = ClassRegex.Match(File.ReadAllText(file));
            if (match.Success)
            {
                var name = match.Groups[1].Value;
                results.Add(new EntityInfo(name, file, name, GetNamespace(file), null));
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(entityRoot, "*", SearchOption.AllDirectories))
        {
            if (IsIgnored(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var match = ClassRegex.Match(File.ReadAllText(file));
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    var relativePath = NormalizeRelativePath(Path.GetRelativePath(entityRoot, Path.GetDirectoryName(file) ?? entityRoot));
                    var displayName = relativePath == "." ? name : $"{relativePath}/{name}";
                    results.Add(new EntityInfo(name, file, displayName, GetNamespace(file), relativePath == "." ? null : relativePath));
                }
            }
        }

        return results
            .OrderBy(entity => entity.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<WebFeatureInfo> GetWebFeatureInfos()
    {
        var root = config.WebFeatureRootPath;
        if (!Directory.Exists(root))
        {
            return [];
        }

        var results = new List<WebFeatureInfo>();
        CollectWebFeatureDirectories(root, root, results);
        return results
            .OrderBy(feature => feature.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void CollectWebFeatureDirectories(string root, string currentDir, List<WebFeatureInfo> results)
    {
        foreach (var dir in Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (IsIgnored(dir))
            {
                continue;
            }

            results.Add(new WebFeatureInfo(
                Path.GetFileName(dir),
                NormalizeRelativePath(Path.GetRelativePath(root, dir)),
                dir));

            CollectWebFeatureDirectories(root, dir, results);
        }
    }

    private DependencyInjectionInfo GetDependencyInjectionInfo()
    {
        var path = config.DependencyInjectionPath;
        if (!File.Exists(path))
        {
            return new DependencyInjectionInfo(path, []);
        }

        var text = File.ReadAllText(path);
        var registrations = DiRegistrationRegex.Matches(text)
            .Select(match => new DiRegistrationInfo(
                match.Groups["service"].Value,
                match.Groups["implementation"].Value,
                match.Groups["lifetime"].Value,
                path))
            .OrderBy(registration => registration.ServiceType, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DependencyInjectionInfo(path, registrations);
    }

    private static IReadOnlyList<TypeCandidate> GetTypesFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path))
            .Select(path => new TypeCandidate(Path.GetFileNameWithoutExtension(path), path))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<TypeCandidate> GetMatchingTypes(string directory, Regex typeRegex)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return EnumerateFiles(directory, "*.cs")
            .Select(path => (Path: path, Match: typeRegex.Match(File.ReadAllText(path))))
            .Where(item => item.Match.Success)
            .Select(item => new TypeCandidate(item.Match.Groups[1].Value, item.Path))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool IsFeatureDirectory(string directory)
    {
        return Directory.Exists(Path.Combine(directory, config.DtoFolderName))
               || Directory.Exists(Path.Combine(directory, config.InterfacesFolderName))
               || Directory.Exists(Path.Combine(directory, config.QueriesFolderName))
               || Directory.Exists(Path.Combine(directory, config.CommandsFolderName))
               || Directory.Exists(Path.Combine(directory, config.ModelsFolderName));
    }

    private (string? Path, QueryServiceImplementationPlacement Placement, int CandidateCount) FindImplementation(string featurePath, string className)
    {
        if (!Directory.Exists(config.QueryServicesPath))
        {
            return (null, QueryServiceImplementationPlacement.Missing, 0);
        }

        var matches = EnumerateFiles(config.QueryServicesPath, "*.cs")
            .Where(path => HasClassName(path, className))
            .ToArray();

        if (matches.Length == 0)
        {
            return (null, QueryServiceImplementationPlacement.Missing, 0);
        }

        if (matches.Length > 1)
        {
            return (null, QueryServiceImplementationPlacement.Ambiguous, matches.Length);
        }

        var expectedPath = Path.GetFullPath(GenerationNaming.GetQueryServiceImplementationPath(config, featurePath, className));
        var actualPath = Path.GetFullPath(matches[0]);

        return string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase)
            ? (matches[0], QueryServiceImplementationPlacement.CanonicalFeaturePath, 1)
            : (matches[0], QueryServiceImplementationPlacement.NonCanonical, 1);
    }

    private bool HasClassName(string path, string className)
    {
        var match = ClassRegex.Match(File.ReadAllText(path));
        return match.Success && match.Groups[1].Value == className;
    }

    private string GetFeatureRoot(string featurePath) =>
        Path.Combine(config.ApplicationFeatureRootPath, featurePath.Replace('/', Path.DirectorySeparatorChar));

    private static string GetNamespace(string path)
    {
        var match = NamespaceRegex.Match(File.ReadAllText(path));
        return match.Success ? match.Groups[1].Value : "";
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path));
    }

    private static bool IsIgnored(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}
