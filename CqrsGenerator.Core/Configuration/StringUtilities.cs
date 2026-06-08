namespace CqrsGenerator.Core.Configuration;

public static class StringUtilities
{
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return name.Length == 1
            ? name.ToLowerInvariant()
            : char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static string StripSuffix(string value, string suffix) =>
        value.EndsWith(suffix, StringComparison.Ordinal) ? value[..^suffix.Length] : value;

    public static string NormalizeFeaturePath(string featurePath) =>
        string.Join('/', featurePath.Split('/', StringSplitOptions.RemoveEmptyEntries));

    public static string ToNamespace(params string[] segments) =>
        string.Join('.', segments.SelectMany(segment => segment.Split('/', StringSplitOptions.RemoveEmptyEntries)));

    public static string ToKebabCase(string value)
    {
        var chars = new List<char>();
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                chars.Add('-');
            }

            chars.Add(char.ToLowerInvariant(current));
        }

        return new string(chars.ToArray());
    }

    public static IReadOnlyList<(string Name, string Type)> ParseRouteParameters(string route)
    {
        var parameters = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(route))
            return parameters;

        var span = route.AsSpan();
        var i = 0;
        while (i < span.Length)
        {
            var openBrace = span[i..].IndexOf('{');
            if (openBrace < 0) break;

            var closeBrace = span[(i + openBrace)..].IndexOf('}');
            if (closeBrace < 0) break;

            var segment = span.Slice(i + openBrace + 1, closeBrace - 1);
            var colonIdx = segment.IndexOf(':');

            string name, type;
            if (colonIdx >= 0)
            {
                name = ToPascalCase(segment[..colonIdx].ToString());
                type = segment[(colonIdx + 1)..].ToString();
                if (type.EndsWith('?')) type = type[..^1];
            }
            else
            {
                name = ToPascalCase(segment.ToString());
                type = "string";
            }

            parameters.Add((name, type));
            i += openBrace + closeBrace + 1;
        }

        return parameters;
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return value.Length == 1
            ? value.ToUpperInvariant()
            : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
