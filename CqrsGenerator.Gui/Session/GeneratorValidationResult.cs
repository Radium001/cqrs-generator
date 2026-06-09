namespace CqrsGenerator.Gui.Session;

public sealed record GeneratorValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static readonly GeneratorValidationResult Valid = new(true, [], []);

    public static GeneratorValidationResult Error(string error) =>
        new(false, [error], []);

    public static GeneratorValidationResult Warning(string warning) =>
        new(true, [], [warning]);
}
