namespace CqrsGenerator.Gui.Models;

public sealed record CommandDependencyOption(string InterfaceName, bool IsGeneratedInSession = false);
