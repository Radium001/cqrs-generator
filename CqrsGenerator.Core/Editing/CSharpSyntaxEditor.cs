using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CqrsGenerator.Core.Editing;

public sealed class CSharpSyntaxEditor
{
    public string AddMethodToInterface(
        string source,
        string interfaceName,
        string returnType,
        string methodName,
        IReadOnlyList<(string Type, string Name)> parameters,
        HashSet<string>? defaultParameters = null)
    {
        var root = ParseRoot(source);
        var interfaceDeclaration = root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .FirstOrDefault(node => node.Identifier.ValueText == interfaceName)
            ?? throw new InvalidOperationException($"Interface '{interfaceName}' was not found.");

        if (interfaceDeclaration.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == methodName))
        {
            throw new InvalidOperationException($"Method '{methodName}' already exists in interface '{interfaceName}'.");
        }

        var newline = DetectNewLine(source);
        var closeBraceLineStart = GetLineStartPosition(source, interfaceDeclaration.CloseBraceToken.SpanStart);
        var closeBraceIsOnOwnLine = string.IsNullOrWhiteSpace(
            source[closeBraceLineStart..interfaceDeclaration.CloseBraceToken.SpanStart]);
        var closeBracePosition = closeBraceIsOnOwnLine
            ? closeBraceLineStart
            : interfaceDeclaration.CloseBraceToken.SpanStart;
        var closeBraceIndent = GetLineIndent(source, closeBraceLineStart);
        var memberIndent = closeBraceIndent + "    ";
        var insertionPrefix = closeBraceIsOnOwnLine ? string.Empty : newline;
        var methodText = $"{insertionPrefix}{memberIndent}{returnType} {methodName}({CreateParametersText(parameters, defaultParameters)});{newline}";

        return source.Insert(closeBracePosition, methodText);
    }

    public string AddMethodToClass(
        string source,
        string className,
        string returnType,
        string methodName,
        IReadOnlyList<(string Type, string Name)> parameters,
        string bodyStatement,
        HashSet<string>? defaultParameters = null,
        bool isAsync = true)
    {
        var root = ParseRoot(source);
        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(node => node.Identifier.ValueText == className)
            ?? throw new InvalidOperationException($"Class '{className}' was not found.");

        if (classDeclaration.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == methodName))
        {
            throw new InvalidOperationException($"Method '{methodName}' already exists in class '{className}'.");
        }

        var newline = DetectNewLine(source);
        var closeBraceLineStart = GetLineStartPosition(source, classDeclaration.CloseBraceToken.SpanStart);
        var closeBraceIsOnOwnLine = string.IsNullOrWhiteSpace(
            source[closeBraceLineStart..classDeclaration.CloseBraceToken.SpanStart]);
        var closeBracePosition = closeBraceIsOnOwnLine
            ? closeBraceLineStart
            : classDeclaration.CloseBraceToken.SpanStart;
        var closeBraceIndent = GetLineIndent(source, closeBraceLineStart);
        var memberIndent = closeBraceIndent + "    ";
        var bodyIndent = memberIndent + "    ";
        var insertionPrefix = closeBraceIsOnOwnLine ? string.Empty : newline;
        var methodText = insertionPrefix + string.Join(newline, new[]
        {
            $"{memberIndent}public {(isAsync ? "async " : string.Empty)}{returnType} {methodName}({CreateParametersText(parameters, defaultParameters)})",
            $"{memberIndent}{{",
            FormatBody(bodyStatement, bodyIndent, newline),
            $"{memberIndent}}}",
            "",
        });

        return source.Insert(closeBracePosition, methodText);
    }

    public string AddScopedRegistration(
        string source,
        string serviceType,
        string implementationType,
        string methodName = "AddInfrastructure")
    {
        var root = ParseRoot(source);
        if (ContainsRegistration(root, serviceType, implementationType))
        {
            throw new InvalidOperationException($"DI registration '{serviceType}, {implementationType}' already exists.");
        }

        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(node => node.Identifier.ValueText == methodName)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        var body = method.Body ?? throw new InvalidOperationException($"Method '{methodName}' does not have a body.");
        var newline = DetectNewLine(source);
        var returnStatement = body.Statements.OfType<ReturnStatementSyntax>().LastOrDefault();
        var insertPosition = returnStatement is null
            ? body.CloseBraceToken.SpanStart
            : GetInsertionPositionBeforeBlankLines(source, returnStatement.SpanStart);
        var indent = returnStatement is null
            ? GetLineIndent(source, body.CloseBraceToken.SpanStart) + "    "
            : GetLineIndent(source, returnStatement.SpanStart);
        var registration = $"{indent}services.AddScoped<{serviceType}, {implementationType}>();{newline}";

        return source.Insert(insertPosition, registration);
    }

    public string ReplaceNamespace(string source, string newNamespace)
    {
        var root = ParseRoot(source);
        var namespaceNode = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()
            ?? throw new InvalidOperationException("Namespace declaration was not found.");

        if (namespaceNode.Name.ToString() == newNamespace)
        {
            return source;
        }

        if (namespaceNode is NamespaceDeclarationSyntax blockScoped)
        {
            return RewriteBlockScopedNamespace(source, blockScoped, newNamespace);
        }

        SyntaxNode rewritten = namespaceNode switch
        {
            FileScopedNamespaceDeclarationSyntax fileScoped => fileScoped.WithName(SyntaxFactory.ParseName(newNamespace)),
            _ => throw new InvalidOperationException("Unsupported namespace declaration.")
        };

        return root.ReplaceNode(namespaceNode, rewritten).ToFullString();
    }

    public string AddUsingIfTypeReferenced(string source, string namespaceToAdd, string typeName)
    {
        if (string.IsNullOrWhiteSpace(namespaceToAdd))
        {
            return source;
        }

        var root = ParseRoot(source);
        var currentNamespace = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        if (string.Equals(currentNamespace, namespaceToAdd, StringComparison.Ordinal))
        {
            return source;
        }

        if (root.Usings.Any(u => string.Equals(u.Name?.ToString(), namespaceToAdd, StringComparison.Ordinal)))
        {
            return source;
        }

        var hasReference = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => identifier.Identifier.ValueText == typeName);
        if (!hasReference)
        {
            return source;
        }

        return AddUsing(source, namespaceToAdd);
    }

    public string AddUsing(string source, string namespaceToAdd)
    {
        if (string.IsNullOrWhiteSpace(namespaceToAdd))
        {
            return source;
        }

        var root = ParseRoot(source);
        var currentNamespace = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        if (string.Equals(currentNamespace, namespaceToAdd, StringComparison.Ordinal))
        {
            return source;
        }

        if (root.Usings.Any(u => string.Equals(u.Name?.ToString(), namespaceToAdd, StringComparison.Ordinal)))
        {
            return source;
        }

        var newline = DetectNewLine(source);
        var insertionText = $"using {namespaceToAdd};{newline}";
        if (root.Usings.Count > 0)
        {
            var insertPosition = root.Usings.Last().FullSpan.End;
            return source.Insert(insertPosition, insertionText);
        }

        var firstMember = root.Members.FirstOrDefault();
        if (firstMember is null)
        {
            return insertionText + source;
        }

        return source.Insert(firstMember.FullSpan.Start, insertionText);
    }

    private static CompilationUnitSyntax ParseRoot(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var diagnostics = root.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (diagnostics.Length > 0)
        {
            var message = string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"C# source is invalid:{Environment.NewLine}{message}");
        }

        return root;
    }

    private static string CreateParametersText(
        IReadOnlyList<(string Type, string Name)> parameters,
        HashSet<string>? defaultParameters = null)
    {
        return string.Join(", ", parameters.Select(parameter =>
            defaultParameters?.Contains(parameter.Name) == true
                ? $"{parameter.Type} {parameter.Name} = default"
                : $"{parameter.Type} {parameter.Name}"));
    }

    private static string FormatBody(string bodyStatement, string bodyIndent, string newline)
    {
        var lines = bodyStatement
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        return string.Join(newline, lines.Select(line =>
            string.IsNullOrWhiteSpace(line)
                ? bodyIndent.TrimEnd()
                : bodyIndent + line.Trim()));
    }

    private static string GetLineIndent(string source, int position)
    {
        var text = SourceText.From(source);
        var line = text.Lines.GetLineFromPosition(position).ToString();
        return new string(line.TakeWhile(char.IsWhiteSpace).ToArray());
    }

    private static int GetLineStartPosition(string source, int position)
    {
        var text = SourceText.From(source);
        return text.Lines.GetLineFromPosition(position).Start;
    }

    private static int GetInsertionPositionBeforeBlankLines(string source, int position)
    {
        var text = SourceText.From(source);
        var lineIndex = text.Lines.IndexOf(position);
        var insertionLineIndex = lineIndex;

        while (insertionLineIndex > 0)
        {
            var previousLine = text.Lines[insertionLineIndex - 1];
            if (!string.IsNullOrWhiteSpace(previousLine.ToString()))
            {
                break;
            }

            insertionLineIndex--;
        }

        return text.Lines[insertionLineIndex].Start;
    }

    private static string DetectNewLine(string source)
    {
        var crlf = source.IndexOf("\r\n", StringComparison.Ordinal);
        if (crlf >= 0)
        {
            return "\r\n";
        }

        return "\n";
    }

    private static string RewriteBlockScopedNamespace(
        string source,
        NamespaceDeclarationSyntax namespaceNode,
        string newNamespace)
    {
        var newline = DetectNewLine(source);
        var indent = GetLineIndent(source, namespaceNode.NamespaceKeyword.SpanStart);
        var header = $"namespace {newNamespace}{newline}{indent}{{";
        var suffix = source[namespaceNode.OpenBraceToken.Span.End..];

        return string.Concat(
            source.AsSpan(0, namespaceNode.NamespaceKeyword.SpanStart),
            header,
            suffix);
    }

    private static bool ContainsRegistration(CompilationUnitSyntax root, string serviceType, string implementationType)
    {
        return root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                    || memberAccess.Name is not GenericNameSyntax genericName
                    || genericName.Identifier.ValueText != "AddScoped"
                    || genericName.TypeArgumentList.Arguments.Count != 2)
                {
                    return false;
                }

                return genericName.TypeArgumentList.Arguments[0].ToString() == serviceType
                       && genericName.TypeArgumentList.Arguments[1].ToString() == implementationType;
            });
    }
}
