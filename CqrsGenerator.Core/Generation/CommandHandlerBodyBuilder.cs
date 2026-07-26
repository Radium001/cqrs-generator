using CqrsGenerator.Core.Configuration;
using Microsoft.CodeAnalysis.CSharp;

namespace CqrsGenerator.Core.Generation;

public sealed record CommandHandlerBodyBuildResult(
    string Body,
    bool IsAsync,
    IReadOnlyList<string> RequiredNamespaces,
    string? Warning);

public static class CommandHandlerBodyBuilder
{
    public static CommandHandlerBodyBuildResult Build(CommandGenerationRequest request, string commandBaseName)
    {
        if (!request.GenerateHandlerBody)
        {
            return Stub();
        }

        var operation = GetOperation(commandBaseName);
        if (operation is null)
        {
            return Fallback(commandBaseName, "the command name does not start with Create, Update, or Delete");
        }

        var repositories = request.HandlerScaffoldContext?.Repositories ?? [];
        if (repositories.Count == 0)
        {
            return Fallback(commandBaseName, "no selected repository contract is available");
        }

        var candidates = repositories
            .SelectMany(repository => operation switch
            {
                "Create" => BuildCreateCandidates(request, repository),
                "Update" => BuildUpdateCandidates(request, repository),
                "Delete" => BuildDeleteCandidates(request, repository),
                _ => [],
            })
            .GroupBy(candidate => candidate.Body, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return candidates.Length switch
        {
            1 => new CommandHandlerBodyBuildResult(
                candidates[0].Body,
                true,
                candidates[0].RequiredNamespaces,
                null),
            0 => Fallback(commandBaseName, GetNoCandidateReason(operation, repositories)),
            _ => Fallback(commandBaseName, $"more than one {operation} method chain matches the command properties"),
        };
    }

    private static IEnumerable<ScaffoldCandidate> BuildCreateCandidates(
        CommandGenerationRequest request,
        CommandHandlerRepositoryContract repository)
    {
        if (repository.Entity is null || string.IsNullOrWhiteSpace(repository.EntityType))
        {
            yield break;
        }

        var factories = repository.Entity.Methods.Where(method =>
            method.IsStatic &&
            method.Name == "Create" &&
            SameType(method.ReturnType, repository.EntityType));
        var addMethods = repository.Methods.Where(method =>
            method.Name == GeneratorConstants.RepoMethodAdd &&
            IsTask(method.ReturnType));

        foreach (var factory in factories)
        {
            if (!TryMapArguments(request, factory.Parameters, null, repository.EntityType, out var factoryArguments))
            {
                continue;
            }

            foreach (var addMethod in addMethods)
            {
                if (!TryMapArguments(request, addMethod.Parameters, "entity", repository.EntityType, out var addArguments))
                {
                    continue;
                }

                var body =
                    $"var entity = {repository.Entity.Name}.Create({string.Join(", ", factoryArguments)});\n" +
                    $"await _{StringUtilities.ToCamelCase(repository.DependencyName)}.{addMethod.Name}({string.Join(", ", addArguments)});";
                yield return new ScaffoldCandidate(body, GetRequiredNamespaces(repository.Entity));
            }
        }
    }

    private static IEnumerable<ScaffoldCandidate> BuildUpdateCandidates(
        CommandGenerationRequest request,
        CommandHandlerRepositoryContract repository)
    {
        if (repository.Entity is null || string.IsNullOrWhiteSpace(repository.EntityType))
        {
            yield break;
        }

        var getMethods = repository.Methods.Where(method =>
            method.Name == GeneratorConstants.RepoMethodGetById &&
            IsTaskOf(method.ReturnType, repository.EntityType));
        var domainMethods = repository.Entity.Methods.Where(method =>
            !method.IsStatic &&
            method.Name == "Update" &&
            SameType(method.ReturnType, "void"));
        var updateMethods = repository.Methods.Where(method =>
            method.Name == GeneratorConstants.RepoMethodUpdate &&
            IsTask(method.ReturnType));

        foreach (var getMethod in getMethods)
        {
            if (!TryMapArguments(request, getMethod.Parameters, null, repository.EntityType, out var getArguments))
            {
                continue;
            }

            foreach (var domainMethod in domainMethods)
            {
                if (!TryMapArguments(request, domainMethod.Parameters, null, repository.EntityType, out var domainArguments))
                {
                    continue;
                }

                foreach (var updateMethod in updateMethods)
                {
                    if (!TryMapArguments(request, updateMethod.Parameters, "entity", repository.EntityType, out var updateArguments))
                    {
                        continue;
                    }

                    var body =
                        $"var entity = await _{StringUtilities.ToCamelCase(repository.DependencyName)}.{getMethod.Name}({string.Join(", ", getArguments)});\n" +
                        $"entity.{domainMethod.Name}({string.Join(", ", domainArguments)});\n" +
                        $"await _{StringUtilities.ToCamelCase(repository.DependencyName)}.{updateMethod.Name}({string.Join(", ", updateArguments)});";
                    yield return new ScaffoldCandidate(body, GetRequiredNamespaces(repository.Entity));
                }
            }
        }
    }

    private static IEnumerable<ScaffoldCandidate> BuildDeleteCandidates(
        CommandGenerationRequest request,
        CommandHandlerRepositoryContract repository)
    {
        foreach (var deleteMethod in repository.Methods.Where(method =>
                     method.Name == GeneratorConstants.RepoMethodDelete &&
                     IsTask(method.ReturnType)))
        {
            if (!TryMapArguments(request, deleteMethod.Parameters, null, repository.EntityType, out var arguments))
            {
                continue;
            }

            yield return new ScaffoldCandidate(
                $"await _{StringUtilities.ToCamelCase(repository.DependencyName)}.{deleteMethod.Name}({string.Join(", ", arguments)});",
                []);
        }
    }

    private static bool TryMapArguments(
        CommandGenerationRequest request,
        IReadOnlyList<PropertySpec> parameters,
        string? entityExpression,
        string? entityType,
        out IReadOnlyList<string> arguments)
    {
        var mapped = new List<string>();
        foreach (var parameter in parameters)
        {
            if (SameType(parameter.Type, GeneratorConstants.CancellationTokenType))
            {
                mapped.Add("cancellationToken");
                continue;
            }

            if (entityExpression is not null && entityType is not null && SameType(parameter.Type, entityType))
            {
                mapped.Add(entityExpression);
                continue;
            }

            var property = request.Properties.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                SameType(candidate.Type, parameter.Type));
            if (property is not null)
            {
                mapped.Add($"request.{property.Name}");
                continue;
            }

            var currentUser = request.Dependencies.FirstOrDefault(dependency =>
                dependency.Type == "ICurrentUserContext");
            if (currentUser is not null &&
                string.Equals(parameter.Name, "userId", StringComparison.OrdinalIgnoreCase) &&
                SameType(parameter.Type, "int"))
            {
                mapped.Add($"_{StringUtilities.ToCamelCase(currentUser.Name)}.UserId");
                continue;
            }

            arguments = [];
            return false;
        }

        arguments = mapped;
        return true;
    }

    private static string GetNoCandidateReason(
        string operation,
        IReadOnlyList<CommandHandlerRepositoryContract> repositories)
    {
        if (operation == "Delete")
        {
            var deleteMethods = repositories
                .SelectMany(repository => repository.Methods)
                .Where(method => method.Name == GeneratorConstants.RepoMethodDelete && IsTask(method.ReturnType))
                .ToArray();
            return deleteMethods.Length == 0
                ? "DeleteAsync was not found in the selected repository contracts"
                : "DeleteAsync parameters do not match the command properties";
        }

        if (operation == "Create")
        {
            if (!repositories.Any(repository => repository.Methods.Any(method =>
                    method.Name == GeneratorConstants.RepoMethodAdd && IsTask(method.ReturnType))))
            {
                return "AddAsync was not found in the selected repository contracts";
            }

            if (!repositories.Any(repository => repository.Entity?.Methods.Any(method =>
                    method.IsStatic && method.Name == "Create") == true))
            {
                return "a compatible static entity Create method was not found";
            }
        }

        if (operation == "Update")
        {
            if (!repositories.Any(repository => repository.Methods.Any(method =>
                    method.Name == GeneratorConstants.RepoMethodGetById && IsTaskOf(method.ReturnType, repository.EntityType ?? ""))))
            {
                return "a compatible GetByIdAsync method was not found";
            }

            if (!repositories.Any(repository => repository.Entity?.Methods.Any(method =>
                    !method.IsStatic && method.Name == "Update") == true))
            {
                return "a compatible entity Update method was not found";
            }

            if (!repositories.Any(repository => repository.Methods.Any(method =>
                    method.Name == GeneratorConstants.RepoMethodUpdate && IsTask(method.ReturnType))))
            {
                return "UpdateAsync was not found in the selected repository contracts";
            }
        }

        return $"{operation} method parameters do not match the command properties";
    }

    private static IReadOnlyList<string> GetRequiredNamespaces(CommandHandlerEntityContract entity) =>
        string.IsNullOrWhiteSpace(entity.Namespace) ? [] : [entity.Namespace];

    private static string? GetOperation(string commandBaseName)
    {
        foreach (var operation in GeneratorConstants.CommandVerbPrefixes)
        {
            if (commandBaseName.StartsWith(operation, StringComparison.Ordinal) &&
                (commandBaseName.Length == operation.Length ||
                 char.IsUpper(commandBaseName[operation.Length]) ||
                 char.IsDigit(commandBaseName[operation.Length])))
            {
                return operation;
            }
        }

        return null;
    }

    private static bool IsTask(string type) => SameType(type, "Task");

    private static bool IsTaskOf(string type, string resultType)
    {
        var syntax = SyntaxFactory.ParseTypeName(type);
        var generic = syntax.DescendantNodesAndSelf()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.ValueText == "Task");
        return generic is not null &&
               generic.Identifier.ValueText == "Task" &&
               generic.TypeArgumentList.Arguments.Count == 1 &&
               SameType(generic.TypeArgumentList.Arguments[0].ToString(), resultType);
    }

    private static bool SameType(string left, string right) =>
        string.Equals(NormalizeType(left), NormalizeType(right), StringComparison.Ordinal);

    private static string NormalizeType(string type)
    {
        var normalized = type.Replace("global::", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .TrimEnd('?');
        var separator = normalized.LastIndexOf('.');
        return separator >= 0 ? normalized[(separator + 1)..] : normalized;
    }

    private static CommandHandlerBodyBuildResult Stub() =>
        new(GeneratorConstants.DefaultStubBody, false, [], null);

    private static CommandHandlerBodyBuildResult Fallback(string commandBaseName, string reason)
    {
        var warning = $"Handler body for '{commandBaseName}' was not generated automatically: {reason}.";
        return new CommandHandlerBodyBuildResult(
            GeneratorConstants.DefaultStubBody,
            false,
            [],
            warning);
    }

    private sealed record ScaffoldCandidate(string Body, IReadOnlyList<string> RequiredNamespaces);
}
