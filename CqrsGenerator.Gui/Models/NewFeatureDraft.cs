namespace CqrsGenerator.Gui.Models;

public sealed record NewFeatureDraft(
    string FeatureName,
    string? Subfolder,
    bool CreateWebFeature);
