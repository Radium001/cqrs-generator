using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class QueryServiceGenerator(
    GeneratorConfig config,
    ScribanTemplateRenderer renderer,
    CSharpSyntaxEditor editor)
{
    public GenerationPlan CreateServicePlan(QueryServiceGenerationRequest request)
    {
        ValidateService(request);

        var plan = new GenerationPlan();
        var featurePath = StringUtilities.NormalizeFeaturePath(request.FeaturePath);
        var interfaceNamespace = StringUtilities.ToNamespace(config.RootNamespace, config.FeatureRoot, featurePath, config.InterfacesFolderName);
        var hasMethod = !string.IsNullOrWhiteSpace(request.InitialReturnType)
                        && !string.IsNullOrWhiteSpace(request.InitialMethodName);
        var methodParameters = request.InitialParameters.Count == 0
            ? $"{GeneratorConstants.CancellationTokenType} {GeneratorConstants.CancellationTokenParamName} = default"
            : string.Join(", ", request.InitialParameters.Select(parameter => $"{parameter.Type} {StringUtilities.ToCamelCase(parameter.Name)}")) + $", {GeneratorConstants.CancellationTokenType} {GeneratorConstants.CancellationTokenParamName} = default";

        var dtoTypeName = request.DtoTypeName ?? ExtractDtoType(request.InitialReturnType);
        var needsCollections = IsCollectionReturnType(request.InitialReturnType);
        var implementationBody = hasMethod && request.GenerateImplementationBody
            ? BuildDapperBody(request.InitialReturnType, dtoTypeName, request.InitialParameters)
            : null;
        var methodTypes = request.InitialParameters
            .Select(parameter => parameter.Type)
            .Append(request.InitialReturnType)
            .ToArray();

        plan.AddCreateFile(
            Path.Combine(config.ApplicationFeatureRootPath, featurePath, config.InterfacesFolderName, $"{request.InterfaceName}.cs"),
            renderer.Render(TemplateNames.QueryServiceInterface, new
            {
                feature_path = featurePath,
                query_service_interface = request.InterfaceName,
                @namespace = interfaceNamespace,
                has_method = hasMethod,
                method_return_type = request.InitialReturnType ?? "",
                method_name = request.InitialMethodName ?? "",
                method_parameters = methodParameters,
                needs_collections = needsCollections,
                usings_block = CSharpTypeMetadataResolver.CreateUsingsBlock(
                    methodTypes,
                    "System.Collections.Generic",
                    "System.Threading",
                    "System.Threading.Tasks"),
                dto_namespace = request.DtoNamespace,
            }));

        plan.AddCreateFile(
            request.ImplementationPath,
            renderer.Render(TemplateNames.QueryServiceImplementation, new
            {
                query_service_implementation = request.ImplementationName,
                query_service_interface = request.InterfaceName,
                interface_namespace = interfaceNamespace,
                @namespace = request.ImplementationNamespace,
                has_method = hasMethod,
                method_return_type = request.InitialReturnType ?? "",
                method_name = request.InitialMethodName ?? "",
                method_parameters = methodParameters,
                implementation_body = implementationBody ?? GeneratorConstants.DefaultStubBody,
                method_is_async = implementationBody is not null,
                needs_collections = needsCollections,
                usings_block = CSharpTypeMetadataResolver.CreateUsingsBlock(
                    methodTypes,
                    "System",
                    "System.Collections.Generic",
                    "System.Threading",
                    "System.Threading.Tasks"),
                dto_namespace = request.DtoNamespace,
            }));

        if (request.AddDependencyInjectionRegistration)
        {
            AddDiRegistration(plan, request.InterfaceName, request.ImplementationName);
        }

        return plan;
    }

    public GenerationPlan AddMethodPlan(QueryServiceMethodGenerationRequest request)
    {
        var plan = new GenerationPlan();
        AddMethodToPlan(plan, request);
        return plan;
    }

    public void AddMethodToPlan(GenerationPlan plan, QueryServiceMethodGenerationRequest request)
    {
        ValidateMethod(request);

        var parameters = CreateMethodParameters(request);
        var defaultParams = new HashSet<string> { "ct" };

        var dtoTypeName = request.DtoTypeName ?? ExtractDtoType(request.ReturnType);
        var bodyStatement = request.GenerateImplementationBody
            ? BuildDapperBody(request.ReturnType, dtoTypeName, request.Parameters)
            : GeneratorConstants.DefaultStubBody;

        try
        {
            plan.TransformFile(
                request.InterfacePath,
                content => AddRequiredUsings(
                    editor.AddMethodToInterface(
                        content,
                        request.InterfaceName,
                        request.ReturnType,
                        request.MethodName,
                        parameters,
                        defaultParams),
                    request.ReturnType,
                    request.DtoNamespace,
                    request.Parameters));
        }
        catch (InvalidOperationException exception)
        {
            plan.AddConflict(request.InterfacePath, exception.Message);
        }

        try
        {
            plan.TransformFile(
                request.ImplementationPath,
                content => AddRequiredUsings(
                    editor.AddMethodToClass(
                        content,
                        request.ImplementationName,
                        request.ReturnType,
                        request.MethodName,
                        parameters,
                        bodyStatement,
                        defaultParams,
                        isAsync: request.GenerateImplementationBody),
                    request.ReturnType,
                    request.DtoNamespace,
                    request.Parameters));
        }
        catch (InvalidOperationException exception)
        {
            plan.AddConflict(request.ImplementationPath, exception.Message);
        }
    }

    private void AddDiRegistration(GenerationPlan plan, string interfaceName, string implementationName)
    {
        try
        {
            plan.TransformFile(
                config.DependencyInjectionPath,
                content => editor.AddScopedRegistration(content, interfaceName, implementationName));
        }
        catch (InvalidOperationException exception)
        {
            plan.AddConflict(config.DependencyInjectionPath, exception.Message);
        }
    }

    private static void ValidateService(QueryServiceGenerationRequest request)
    {
        CSharpNameValidator.EnsureFeaturePath(request.FeaturePath);
        CSharpNameValidator.EnsureIdentifier(request.InterfaceName, nameof(request.InterfaceName));
        CSharpNameValidator.EnsureIdentifier(request.ImplementationName, nameof(request.ImplementationName));
        if (string.IsNullOrWhiteSpace(request.ImplementationPath))
        {
            throw new ArgumentException("Implementation path is required.", nameof(request.ImplementationPath));
        }

        if (string.IsNullOrWhiteSpace(request.ImplementationNamespace))
        {
            throw new ArgumentException("Implementation namespace is required.", nameof(request.ImplementationNamespace));
        }
        if (!string.IsNullOrWhiteSpace(request.InitialReturnType))
        {
            CSharpTypeMetadataResolver.EnsureValid(request.InitialReturnType, nameof(request.InitialReturnType));
        }
        foreach (var parameter in request.InitialParameters)
        {
            CSharpNameValidator.EnsureIdentifier(parameter.Name, nameof(parameter.Name));
            CSharpTypeMetadataResolver.EnsureValid(parameter.Type, nameof(parameter.Type));
        }
    }

    private static void ValidateMethod(QueryServiceMethodGenerationRequest request)
    {
        CSharpNameValidator.EnsureIdentifier(request.InterfaceName, nameof(request.InterfaceName));
        CSharpNameValidator.EnsureIdentifier(request.ImplementationName, nameof(request.ImplementationName));
        CSharpNameValidator.EnsureIdentifier(request.MethodName, nameof(request.MethodName));
        if (string.IsNullOrWhiteSpace(request.ReturnType))
        {
            throw new ArgumentException("Return type is required.", nameof(request.ReturnType));
        }
        CSharpTypeMetadataResolver.EnsureValid(request.ReturnType, nameof(request.ReturnType));
        foreach (var parameter in request.Parameters)
        {
            CSharpNameValidator.EnsureIdentifier(parameter.Name, nameof(parameter.Name));
            CSharpTypeMetadataResolver.EnsureValid(parameter.Type, nameof(parameter.Type));
        }
    }

    private static (string Type, string Name)[] CreateMethodParameters(QueryServiceMethodGenerationRequest request) =>
        request.Parameters
            .Select(parameter => (parameter.Type, Name: StringUtilities.ToCamelCase(parameter.Name)))
            .Append((GeneratorConstants.CancellationTokenType, GeneratorConstants.CancellationTokenParamName))
            .ToArray();

    private static string BuildDapperBody(string? returnType, string? dtoType, IReadOnlyList<PropertySpec> parameters)
    {
        var method = IsCollectionReturnType(returnType)
            ? "QueryAsync"
            : IsBooleanType(dtoType)
                ? "ExecuteScalarOrDefaultAsync"
                : "QueryFirstOrDefaultAsync";
        var parameterArgument = parameters.Count == 0
            ? string.Empty
            : $", new {{ {string.Join(", ", parameters.Select(parameter => StringUtilities.ToCamelCase(parameter.Name)))} }}";
        var cancellationArgument = parameters.Count == 0 ? ", ct: ct" : ", ct";

        return $"var sql = \"SELECT * FROM ... WHERE ...\";\n            return await ExecuteAsync(x => x.{method}<{dtoType}>(sql{parameterArgument}{cancellationArgument}));";
    }

    private string AddRequiredUsings(
        string content,
        string returnType,
        string dtoNamespace,
        IReadOnlyList<PropertySpec> parameters)
    {
        content = editor.AddUsing(content, "System");
        content = editor.AddUsing(content, "System.Threading");
        content = editor.AddUsing(content, "System.Threading.Tasks");

        if (IsCollectionReturnType(returnType))
        {
            content = editor.AddUsing(content, "System.Collections.Generic");
        }

        foreach (var @namespace in CSharpTypeMetadataResolver.GetNamespaces(
                     parameters.Select(parameter => parameter.Type).Append(returnType)))
        {
            content = editor.AddUsing(content, @namespace);
        }

        if (!string.IsNullOrWhiteSpace(dtoNamespace))
        {
            content = editor.AddUsing(content, dtoNamespace);
        }

        return content;
    }

    private static bool IsCollectionReturnType(string? returnType) =>
        returnType?.Contains(GeneratorConstants.ListWrapperPrefix, StringComparison.Ordinal) == true
        || returnType?.Contains(GeneratorConstants.EnumerableWrapperPrefix, StringComparison.Ordinal) == true;

    private static bool IsBooleanType(string? typeName) =>
        string.Equals(typeName, "bool", StringComparison.Ordinal)
        || string.Equals(typeName, "System.Boolean", StringComparison.Ordinal);

    private static string? ExtractDtoType(string? returnType)
    {
        if (string.IsNullOrWhiteSpace(returnType))
        {
            return null;
        }

        var start = returnType.IndexOf('<');
        var end = returnType.LastIndexOf('>');
        if (start > 0 && end > start)
        {
            return returnType.Substring(start + 1, end - start - 1).Replace(GeneratorConstants.EnumerableWrapperPrefix, "").Replace(GeneratorConstants.ListWrapperPrefix, "").TrimEnd('>');
        }

        return null;
    }
}
