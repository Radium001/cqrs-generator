namespace CqrsGenerator.Gui.Session.States;

public sealed class RepositoryGeneratorState
{
    public string FeaturePath { get; set; } = string.Empty;

    public string InterfaceName { get; set; } = string.Empty;

    public string ImplementationName { get; set; } = string.Empty;

    public Guid? EntityNodeId { get; set; }

    public bool AddDependencyInjectionRegistration { get; set; } = true;
}
