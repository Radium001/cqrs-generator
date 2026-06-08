using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class QueryGenerator(GeneratorConfig config, ScribanTemplateRenderer renderer)
{
    public GenerationPlan CreatePlan(QueryGenerationRequest request)
    {
        Validate(request);

        var plan = new GenerationPlan();
        var featurePath = StringUtilities.NormalizeFeaturePath(request.FeaturePath);
        var queryBaseName = StringUtilities.StripSuffix(request.QueryName, GeneratorConstants.QuerySuffix);
        var queryType = string.Format(GeneratorConstants.QueryClassPattern, queryBaseName);
        var handlerType = string.Format(GeneratorConstants.HandlerClassPattern, queryBaseName);
        var responseType = GetResponseType(request.DtoName, request.ResponseShape);
        var queryDirectory = Path.Combine(GetFeatureRoot(featurePath), config.QueriesFolderName, queryBaseName);

        var featureNamespace = StringUtilities.ToNamespace(config.RootNamespace, config.FeatureRoot, featurePath);
        var queryNamespace = StringUtilities.ToNamespace(featureNamespace, config.QueriesFolderName, queryBaseName);
        var dtoNamespace = request.DtoNamespace;
        var serviceNamespace = StringUtilities.ToNamespace(featureNamespace, config.InterfacesFolderName);

        plan.AddFile(
            Path.Combine(queryDirectory, $"{queryType}.cs"),
            renderer.Render(TemplateNames.Query, new
            {
                @namespace = queryNamespace,
                query_type = queryType,
                response_type = responseType,
                usings_block = CreateQueryUsings(request.ResponseShape, dtoNamespace),
                has_properties = request.Properties.Count > 0,
                constructor_parameters = string.Join(", ", request.Properties.Select(ToConstructorParameter)),
                constructor_assignments = string.Join("", request.Properties.Select(property => $"            {property.Name} = {StringUtilities.ToCamelCase(property.Name)};\n")),
                properties_block = string.Join("\n\n", request.Properties.Select(property => $"        public {property.Type} {property.Name} {{ get; }}")),
            }));

        var hasService = request.ServiceInterfaceName is not null;
        var serviceField = request.ServiceInterfaceName is null ? "" : $"_{StringUtilities.ToCamelCase(GenerationNaming.ToDependencyName(request.ServiceInterfaceName))}";
        var serviceParameter = request.ServiceInterfaceName is null ? "" : StringUtilities.ToCamelCase(GenerationNaming.ToDependencyName(request.ServiceInterfaceName));

        var handlerBody = hasService && request.GenerateHandlerBody && request.ServiceMethodName is not null
            ? BuildHandlerBody(serviceField, request.ServiceMethodName, request.Properties)
            : GeneratorConstants.DefaultStubBody;

        plan.AddFile(
            Path.Combine(queryDirectory, $"{handlerType}.cs"),
            renderer.Render(TemplateNames.Handler, new
            {
                @namespace = queryNamespace,
                query_type = queryType,
                handler_type = handlerType,
                response_type = responseType,
                needs_collections = request.ResponseShape is ResponseShape.List or ResponseShape.Enumerable,
                dto_namespace = dtoNamespace,
                service_namespace = request.ServiceInterfaceName is null ? "" : serviceNamespace,
                has_service = request.ServiceInterfaceName is not null,
                service_type = request.ServiceInterfaceName ?? "",
                service_field = serviceField,
                service_parameter = serviceParameter,
                handler_body = handlerBody,
            }));

        return plan;
    }

    private static void Validate(QueryGenerationRequest request)
    {
        CSharpNameValidator.EnsureFeaturePath(request.FeaturePath);
        CSharpNameValidator.EnsureIdentifier(StringUtilities.StripSuffix(request.QueryName, GeneratorConstants.QuerySuffix), nameof(request.QueryName));
        CSharpNameValidator.EnsureTypeName(request.DtoName, nameof(request.DtoName));

        if (request.ServiceInterfaceName is not null)
        {
            CSharpNameValidator.EnsureIdentifier(request.ServiceInterfaceName, nameof(request.ServiceInterfaceName));
        }

        foreach (var property in request.Properties)
        {
            CSharpNameValidator.EnsureIdentifier(property.Name, nameof(property.Name));
            if (string.IsNullOrWhiteSpace(property.Type))
            {
                throw new ArgumentException("Property type is required.", nameof(property.Type));
            }
        }
    }

    private string GetFeatureRoot(string featurePath) =>
        Path.Combine(config.ApplicationFeatureRootPath, featurePath.Replace('/', Path.DirectorySeparatorChar));

    private static string GetResponseType(string dtoName, ResponseShape shape) => shape switch
    {
        ResponseShape.Single => dtoName,
        ResponseShape.List => $"List<{dtoName}>",
        ResponseShape.Enumerable => $"IEnumerable<{dtoName}>",
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown response shape."),
    };

    private static string CreateQueryUsings(ResponseShape responseShape, string dtoNamespace)
    {
        var usings = new List<string>();
        if (responseShape is ResponseShape.List or ResponseShape.Enumerable)
        {
            usings.Add("using System.Collections.Generic;");
        }

        if (!string.IsNullOrWhiteSpace(dtoNamespace))
        {
            usings.Add($"using {dtoNamespace};");
        }

        return string.Join("\n", usings);
    }

    private static string ToConstructorParameter(PropertySpec property) =>
        $"{property.Type} {StringUtilities.ToCamelCase(property.Name)}";

    private static string BuildHandlerBody(string serviceField, string serviceMethodName, IReadOnlyList<PropertySpec> properties)
    {
        var forwardedParams = string.Join(", ", properties.Select(p => $"request.{p.Name}"));
        if (forwardedParams.Length > 0)
        {
            forwardedParams += ", ";
        }

        return $"return await {serviceField}.{serviceMethodName}({forwardedParams}cancellationToken);";
    }
}
