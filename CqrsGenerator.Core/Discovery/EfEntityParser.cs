using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CqrsGenerator.Core.Discovery;

public static class EfEntityParser
{
    public static List<EfEntityProperty> Parse(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string text;
        try
        {
            text = File.ReadAllText(filePath);
        }
        catch
        {
            return [];
        }

        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetCompilationUnitRoot();
        var diagnostics = root.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (diagnostics.Length > 0)
            return [];

        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDecl is null)
            return [];

        var result = new List<EfEntityProperty>();

        foreach (var prop in classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (prop.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)))
                continue;

            if (prop.Type is GenericNameSyntax)
                continue;

            if (prop.Type is ArrayTypeSyntax)
                continue;

            var typeName = prop.Type.ToString();
            result.Add(new EfEntityProperty(prop.Identifier.ValueText, typeName));
        }

        return result;
    }

    public static string ToDomainName(string efName)
    {
        if (efName.StartsWith("Id") && efName.Length > 2 && char.IsUpper(efName[2]))
            return efName[2..] + "Id";
        return efName;
    }
}
