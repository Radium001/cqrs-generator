using CqrsGenerator.Core.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CqrsGenerator.Core.Discovery;

internal static class CSharpContractParser
{
    public static IReadOnlyList<CommandHandlerMethodContract> ParseInterfaceMethods(string path, string interfaceName)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot();
        var declaration = root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.ValueText == interfaceName);

        return declaration is null
            ? []
            : declaration.Members.OfType<MethodDeclarationSyntax>().Select(ToContract).ToArray();
    }

    public static IReadOnlyList<CommandHandlerMethodContract> ParsePublicClassMethods(string path, string className)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot();
        var declaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.ValueText == className);

        return declaration is null
            ? []
            : declaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword)))
                .Select(ToContract)
                .ToArray();
    }

    public static string? InferRepositoryEntityName(IReadOnlyList<CommandHandlerMethodContract> methods)
    {
        var getById = methods.FirstOrDefault(method => method.Name == GeneratorConstants.RepoMethodGetById);
        var getByIdEntity = getById is null ? null : ExtractTaskResultType(getById.ReturnType);
        if (getByIdEntity is not null)
        {
            return getByIdEntity;
        }

        var addEntity = methods
            .Where(method => method.Name == GeneratorConstants.RepoMethodAdd)
            .SelectMany(method => method.Parameters)
            .FirstOrDefault(parameter =>
                !IsCancellationToken(parameter.Type) &&
                !IsLikelyScalar(parameter.Type));
        if (addEntity is not null)
        {
            return GetSimpleTypeName(addEntity.Type);
        }

        return null;
    }

    private static CommandHandlerMethodContract ToContract(MethodDeclarationSyntax method)
    {
        return new CommandHandlerMethodContract(
            method.Identifier.ValueText,
            method.ReturnType.ToString(),
            method.ParameterList.Parameters
                .Select(parameter => new PropertySpec(
                    parameter.Type?.ToString() ?? "",
                    parameter.Identifier.ValueText))
                .ToArray(),
            method.Modifiers.Any(token => token.IsKind(SyntaxKind.StaticKeyword)));
    }

    private static string? ExtractTaskResultType(string returnType)
    {
        var type = SyntaxFactory.ParseTypeName(returnType);
        if (type is not GenericNameSyntax generic ||
            generic.Identifier.ValueText != "Task" ||
            generic.TypeArgumentList.Arguments.Count != 1)
        {
            return null;
        }

        return GetSimpleTypeName(generic.TypeArgumentList.Arguments[0].ToString());
    }

    private static string GetSimpleTypeName(string type)
    {
        var normalized = type.Trim().TrimEnd('?');
        var separator = normalized.LastIndexOf('.');
        return separator >= 0 ? normalized[(separator + 1)..] : normalized;
    }

    private static bool IsCancellationToken(string type) =>
        GetSimpleTypeName(type) == GeneratorConstants.CancellationTokenType;

    private static bool IsLikelyScalar(string type)
    {
        var simpleName = GetSimpleTypeName(type);
        return simpleName is
            "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or
            "long" or "ulong" or "float" or "double" or "decimal" or "char" or
            "string" or "Guid" or "DateTime" or "DateTimeOffset" or "TimeSpan";
    }
}
