namespace CqrsGenerator.Gui.Session.States;

public sealed class FeatureGeneratorState
{
    public string FeatureName { get; set; } = string.Empty;

    public string? Subfolder { get; set; }

    public bool CreateWebFeature { get; set; }
}
