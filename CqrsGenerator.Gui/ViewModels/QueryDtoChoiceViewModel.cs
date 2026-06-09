using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Gui.Session;

namespace CqrsGenerator.Gui.ViewModels;

public sealed class QueryDtoChoiceViewModel
{
    public QueryDtoChoiceViewModel(
        string name,
        string displayName,
        string namespaceName,
        DtoLocationKind locationKind,
        string? ownerQueryName = null,
        string? path = null,
        bool isSelectable = true,
        string? selectionBlockedReason = null,
        bool isRuntime = false)
    {
        Name = name;
        DisplayName = displayName;
        Namespace = namespaceName;
        LocationKind = locationKind;
        OwnerQueryName = ownerQueryName;
        Path = path;
        IsSelectable = isSelectable;
        SelectionBlockedReason = selectionBlockedReason;
        IsRuntime = isRuntime;
    }

    public QueryDtoChoiceViewModel(
        ArtifactRef reference,
        string displayName,
        string namespaceName,
        DtoLocationKind locationKind,
        string? ownerQueryName = null,
        string? path = null,
        bool isSelectable = true,
        string? selectionBlockedReason = null)
        : this(
            reference.Name,
            displayName,
            namespaceName,
            locationKind,
            ownerQueryName,
            path,
            isSelectable,
            selectionBlockedReason,
            reference.IsFromSession)
    {
        Ref = reference;
    }

    public ArtifactRef? Ref { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string Namespace { get; }

    public DtoLocationKind LocationKind { get; }

    public string? OwnerQueryName { get; }

    public string? Path { get; }

    public bool IsSelectable { get; }

    public string? SelectionBlockedReason { get; }

    public bool IsRuntime { get; }

    public bool IsFromSession => Ref?.IsFromSession ?? IsRuntime;

    public Guid? NodeId => Ref?.NodeId;

    public bool IsShared => LocationKind == DtoLocationKind.SharedFeatureDto;

    public bool IsLocal => LocationKind == DtoLocationKind.LocalQueryDto;

    public override string ToString() => DisplayName;
}
