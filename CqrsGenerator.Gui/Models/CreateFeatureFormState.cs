namespace CqrsGenerator.Gui.Models;

public sealed record CreateFeatureFormState(
    string? FeatureName,
    string? Subfolder,
    bool CreateWebFeature);
