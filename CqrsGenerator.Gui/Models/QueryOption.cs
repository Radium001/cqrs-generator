using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.Models;

public sealed record QueryOption(
    string Name,
    string ResultTypeName,
    ResponseShape Shape,
    bool IsGeneratedInSession,
    ArtifactRef? Ref = null)
{
    public Guid? NodeId => Ref?.NodeId;
}
