using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class QueryDtoChoiceViewModel
{
    public QueryDtoChoiceViewModel(
        ArtifactRef reference,
        string displayName,
        string namespaceName,
        DtoLocationKind locationKind,
        string? ownerQueryName = null,
        string? path = null,
        bool isSelectable = true,
        string? selectionBlockedReason = null)
    {
        Ref = reference;
        Name = reference.Name;
        DisplayName = displayName;
        Namespace = namespaceName;
        LocationKind = locationKind;
        OwnerQueryName = ownerQueryName;
        Path = path;
        IsSelectable = isSelectable;
        SelectionBlockedReason = selectionBlockedReason;
    }

    public ArtifactRef Ref { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string Namespace { get; }

    public DtoLocationKind LocationKind { get; }

    public string? OwnerQueryName { get; }

    public string? Path { get; }

    public bool IsSelectable { get; }

    public string? SelectionBlockedReason { get; }

    public bool IsFromSession => Ref.IsFromSession;

    public Guid? NodeId => Ref.NodeId;

    public bool IsShared => LocationKind == DtoLocationKind.SharedFeatureDto;

    public bool IsLocal => LocationKind == DtoLocationKind.LocalQueryDto;

    public override string ToString() => DisplayName;
}
