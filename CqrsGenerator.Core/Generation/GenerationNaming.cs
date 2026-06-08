using CqrsGenerator.Core.Configuration;

namespace CqrsGenerator.Core.Generation;

public static class GenerationNaming
{
    public static string ToFeatureBaseName(string featurePath) =>
        featurePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? featurePath;

    public static string GetResponseType(string typeName, ResponseShape shape) => shape switch
    {
        ResponseShape.List => $"List<{typeName}>",
        ResponseShape.Enumerable => $"IEnumerable<{typeName}>",
        _ => typeName,
    };

    public static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length == 1 ? value.ToUpperInvariant() : char.ToUpperInvariant(value[0]) + value[1..];
    }

    public static string ToQueryNamespace(GeneratorConfig config, string featurePath, string queryName)
    {
        var queryBaseName = StringUtilities.StripSuffix(queryName, GeneratorConstants.QuerySuffix);
        return StringUtilities.ToNamespace(
            config.RootNamespace,
            config.FeatureRoot,
            StringUtilities.NormalizeFeaturePath(featurePath),
            config.QueriesFolderName,
            queryBaseName);
    }

    public static string ToCommandNamespace(GeneratorConfig config, string featurePath, string commandName)
    {
        var commandBaseName = StringUtilities.StripSuffix(commandName, GeneratorConstants.CommandSuffix);
        return StringUtilities.ToNamespace(
            config.RootNamespace,
            config.FeatureRoot,
            StringUtilities.NormalizeFeaturePath(featurePath),
            config.CommandsFolderName,
            commandBaseName);
    }

    public static string ToDtoNamespace(GeneratorConfig config, string featurePath) =>
        StringUtilities.ToNamespace(
            config.RootNamespace,
            config.FeatureRoot,
            StringUtilities.NormalizeFeaturePath(featurePath),
            config.DtoFolderName);

    public static string GetQueryServiceImplementationPath(GeneratorConfig config, string featurePath, string implementationName)
    {
        var parentFeaturePath = GetQueryServiceParentFeaturePath(featurePath);
        return string.IsNullOrWhiteSpace(parentFeaturePath)
            ? Path.Combine(config.QueryServicesPath, $"{implementationName}.cs")
            : Path.Combine(
                config.QueryServicesPath,
                parentFeaturePath.Replace('/', Path.DirectorySeparatorChar),
                $"{implementationName}.cs");
    }

    public static string GetQueryServiceImplementationNamespace(GeneratorConfig config, string featurePath)
    {
        var parentFeaturePath = GetQueryServiceParentFeaturePath(featurePath);
        return string.IsNullOrWhiteSpace(parentFeaturePath)
            ? GeneratorConstants.InfrastructureDataQueryServicesNamespace
            : StringUtilities.ToNamespace(
                config.Conventions.InfrastructureRootNamespace,
                "Data",
                config.QueryServicesFolderName,
                parentFeaturePath);
    }

    public static string ToLocalQueryDtoNamespace(GeneratorConfig config, string featurePath, string queryName)
    {
        var queryBaseName = StringUtilities.StripSuffix(queryName, GeneratorConstants.QuerySuffix);
        return StringUtilities.ToNamespace(
            config.RootNamespace,
            config.FeatureRoot,
            StringUtilities.NormalizeFeaturePath(featurePath),
            config.QueriesFolderName,
            queryBaseName);
    }

    public static string ToDependencyName(string interfaceType) =>
        interfaceType.StartsWith("I") && interfaceType.Length > 1
            ? interfaceType[1..]
            : interfaceType;

    public static string GetQueryServiceInterfaceName(string featureBaseName) =>
        string.Format(GeneratorConstants.QueryServiceInterfacePattern, featureBaseName);

    public static string GetQueryServiceImplementationName(string featureBaseName) =>
        string.Format(GeneratorConstants.QueryServiceImplementationPattern, featureBaseName);

    public static string GetRepositoryInterfaceName(string entityName) =>
        string.Format(GeneratorConstants.RepositoryInterfacePattern, entityName);

    public static string GetRepositoryImplementationName(string entityName) =>
        string.Format(GeneratorConstants.RepositoryImplementationPattern, entityName);

    private static string GetQueryServiceParentFeaturePath(string featurePath)
    {
        var segments = StringUtilities.NormalizeFeaturePath(featurePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return string.Empty;
        }

        return string.Join('/', segments[..^1]);
    }
}
