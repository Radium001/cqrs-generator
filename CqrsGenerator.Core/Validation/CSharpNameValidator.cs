using Microsoft.CodeAnalysis.CSharp;

namespace CqrsGenerator.Core.Validation;

public static class CSharpNameValidator
{
    public static void EnsureIdentifier(string value, string label)
    {
        if (!IsIdentifier(value))
        {
            throw new ArgumentException($"{label} must be a valid C# identifier.", label);
        }
    }

    public static void EnsureTypeName(string value, string label)
    {
        if (!IsTypeName(value))
        {
            throw new ArgumentException($"{label} must be a valid C# type name.", label);
        }
    }

    public static void EnsureFeaturePath(string featurePath)
    {
        if (string.IsNullOrWhiteSpace(featurePath))
        {
            throw new ArgumentException("Feature path is required.", nameof(featurePath));
        }

        var segments = featurePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Feature path contains unsafe segments.", nameof(featurePath));
        }

        foreach (var segment in segments)
        {
            EnsureIdentifier(segment, nameof(featurePath));
        }
    }

    public static string? NormalizeOptionalRelativePath(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Path.IsPathRooted(path))
        {
            throw new ArgumentException($"{label} must be a relative path.", label);
        }

        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException($"{label} contains unsafe path segments.", label);
        }

        foreach (var segment in segments)
        {
            EnsureIdentifier(segment, label);
        }

        return string.Join('/', segments);
    }

    public static bool IsIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!SyntaxFacts.IsIdentifierStartCharacter(value[0]))
            return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!SyntaxFacts.IsIdentifierPartCharacter(value[i]))
                return false;
        }

        return SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;
    }

    public static bool IsTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (SyntaxFacts.IsValidIdentifier(value) && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None)
            return true;

        var kind = SyntaxFacts.GetKeywordKind(value);
        return kind != SyntaxKind.None && SyntaxFacts.IsPredefinedType(kind);
    }
}
