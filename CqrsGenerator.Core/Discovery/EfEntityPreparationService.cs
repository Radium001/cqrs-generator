using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Discovery;

public sealed record EfEntityCandidate(
    string Name,
    string Path,
    IReadOnlyList<EfEntityProperty> Properties);

public sealed record PreparedEfEntitySelection(
    IReadOnlyList<PropertySpec> Properties,
    IReadOnlyList<(string DomainName, string EfName)> EfMappingFields);

public sealed class EfEntityPreparationService
{
    public IReadOnlyList<EfEntityCandidate> Discover(GeneratorConfig config)
    {
        var entitiesDir = Path.Combine(config.TargetRootPath, "Infrastructure", "Data", "Entities");
        if (!Directory.Exists(entitiesDir))
        {
            return [];
        }

        return Directory.GetFiles(entitiesDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new EfEntityCandidate(
                Path.GetFileNameWithoutExtension(path) ?? string.Empty,
                path,
                EfEntityParser.Parse(path)))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name) && char.IsUpper(candidate.Name[0]))
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public PreparedEfEntitySelection Prepare(
        EfEntityCandidate candidate,
        IReadOnlyList<string> selectedEfPropertyNames,
        bool renameIdentifierProperties)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(selectedEfPropertyNames);

        var selectedNames = selectedEfPropertyNames.ToHashSet(StringComparer.Ordinal);
        var mappings = candidate.Properties
            .Where(property => selectedNames.Contains(property.EfName))
            .Select(property =>
            {
                var domainName = renameIdentifierProperties
                    ? EfEntityParser.ToDomainName(property.EfName)
                    : property.EfName;
                return (DomainName: domainName, property.EfName, property.EfType);
            })
            .ToArray();

        return new PreparedEfEntitySelection(
            mappings.Select(mapping => new PropertySpec(MapEfType(mapping.EfType), mapping.DomainName)).ToArray(),
            mappings.Select(mapping => (mapping.DomainName, mapping.EfName)).ToArray());
    }

    public static string MapEfType(string efType)
    {
        var shortName = efType.Contains('.') ? efType.Split('.').Last() : efType;
        return shortName switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "String" => "string",
            "Boolean" => "bool",
            "DateTime" => "DateTime",
            "Decimal" => "decimal",
            "Double" => "double",
            "Single" => "float",
            "Byte" => "byte",
            "Guid" => "Guid",
            _ => shortName,
        };
    }
}
