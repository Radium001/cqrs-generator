using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Cli.Interactive;

public static class PropertyInputParser
{
    public static PropertySpec? Parse(string line)
    {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        return new PropertySpec(string.Join(' ', parts[..^1]), parts[^1]);
    }
}
