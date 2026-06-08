namespace CqrsGenerator.Core.Validation;

public sealed record ArchitectureWarning(
    string Code,
    string Severity,
    string Title,
    string Message,
    string? Source);
