using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record AddDtoFormState(
    string? FeatureName,
    string? FeaturePath,
    string DtoName,
    IReadOnlyList<PropertySpec> Properties,
    string? WebFeaturePath,
    string? Subfolder);
