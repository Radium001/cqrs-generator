namespace CqrsGenerator.Gui.Models;

public enum QueryServiceSuggestionMode
{
    CreateNew,
    UpdateExisting,
    Blocked,
}

public sealed record QueryServiceSuggestion(
    string InterfaceName,
    string ImplementationName,
    string? InterfacePath,
    string? ImplementationPath,
    QueryServiceSuggestionMode Mode,
    AutoItemStatus Status);
