using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Workflows;

public abstract record QueryDtoSelection(string DtoName);

public sealed record CreateLocalQueryDtoSelection(
    string DtoName,
    IReadOnlyList<PropertySpec> Properties,
    string OwnerQueryName)
    : QueryDtoSelection(DtoName);

public sealed record UseSharedFeatureDtoSelection(
    string DtoName,
    string DtoNamespace,
    string DtoPath)
    : QueryDtoSelection(DtoName);

public sealed record PromoteLocalQueryDtoSelection(
    string DtoName,
    string SourcePath,
    string SourceNamespace,
    string OwnerQueryName,
    string TargetFeaturePath)
    : QueryDtoSelection(DtoName);
