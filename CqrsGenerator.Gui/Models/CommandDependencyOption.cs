using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.Models;

public sealed record CommandDependencyOption(
    string InterfaceName,
    bool IsGeneratedInSession = false,
    ArtifactRef? Ref = null)
{
    public Guid? NodeId => Ref?.NodeId;
}
