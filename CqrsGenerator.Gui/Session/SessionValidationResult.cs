namespace CqrsGenerator.Gui.Session;

public sealed record SessionValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
