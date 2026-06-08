using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Gui.Models;

public sealed record QueryDtoSelectionState(
    string DtoName,
    string? DtoNamespace,
    string? DtoPath,
    DtoLocationKind LocationKind,
    string? OwnerQueryName,
    bool CreateNewLocalDto,
    bool IsSelectable,
    string? SelectionBlockedReason);
