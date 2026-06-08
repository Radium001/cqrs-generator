namespace CqrsGenerator.Cli.Interactive;

public sealed record DtoChoice(string Name, bool Create);

public sealed record QueryServiceChoice(
    string InterfaceName,
    string ImplementationName,
    string? InterfacePath,
    string? ImplementationPath,
    bool CreateNew);
