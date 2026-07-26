using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CqrsGenerator.Core.Generation;

public static class CSharpTypeMetadataResolver
{
    private static readonly IReadOnlyDictionary<string, string> KnownNamespaces =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DateTime"] = "System",
            ["DateTimeOffset"] = "System",
            ["Guid"] = "System",
            ["TimeSpan"] = "System",
            ["Uri"] = "System",
            ["IEnumerable"] = "System.Collections.Generic",
            ["ICollection"] = "System.Collections.Generic",
            ["IReadOnlyCollection"] = "System.Collections.Generic",
            ["IList"] = "System.Collections.Generic",
            ["IReadOnlyList"] = "System.Collections.Generic",
            ["List"] = "System.Collections.Generic",
            ["ISet"] = "System.Collections.Generic",
            ["HashSet"] = "System.Collections.Generic",
            ["IDictionary"] = "System.Collections.Generic",
            ["Dictionary"] = "System.Collections.Generic",
            ["Queue"] = "System.Collections.Generic",
            ["Stack"] = "System.Collections.Generic",
            ["CancellationToken"] = "System.Threading",
            ["Task"] = "System.Threading.Tasks",
            ["ValueTask"] = "System.Threading.Tasks",
            ["IQueryable"] = "System.Linq",
            ["IOrderedEnumerable"] = "System.Linq",
        };

    public static void EnsureValid(string typeName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Type is required.", parameterName);
        }

        var syntax = SyntaxFactory.ParseTypeName(typeName, consumeFullText: true);
        if (syntax.ContainsDiagnostics || syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            throw new ArgumentException($"{parameterName} must be a valid C# type.", parameterName);
        }
    }

    public static IReadOnlyList<string> GetNamespaces(IEnumerable<string?> typeNames)
    {
        var namespaces = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var typeName in typeNames.Where(typeName => !string.IsNullOrWhiteSpace(typeName)))
        {
            var syntax = SyntaxFactory.ParseTypeName(typeName!, consumeFullText: true);
            foreach (var name in syntax.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
            {
                if (KnownNamespaces.TryGetValue(name.Identifier.ValueText, out var @namespace))
                {
                    namespaces.Add(@namespace);
                }
            }
        }

        return namespaces.ToArray();
    }

    public static string CreateUsingsBlock(
        IEnumerable<string?> typeNames,
        params string[] excludedNamespaces) =>
        string.Join(
            "\n",
            GetNamespaces(typeNames)
                .Except(excludedNamespaces, StringComparer.Ordinal)
                .Select(@namespace => $"using {@namespace};"));
}
