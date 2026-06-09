namespace CqrsGenerator.Gui.Session;

public sealed record GeneratorCreationContext(
    string? FeaturePath = null,
    Guid? ParentNodeId = null);
