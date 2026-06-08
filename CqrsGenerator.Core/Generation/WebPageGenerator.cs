using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class WebPageGenerator(GeneratorConfig config, ScribanTemplateRenderer renderer)
{
    public GenerationPlan CreatePlan(WebPageGenerationRequest request)
    {
        var plan = new GenerationPlan();
        ApplyToPlan(plan, request);
        return plan;
    }

    public void ApplyToPlan(GenerationPlan plan, WebPageGenerationRequest request)
    {
        Validate(request);

        var webFeaturePath = StringUtilities.NormalizeFeaturePath(request.WebFeaturePath);
        var featureRoot = Path.Combine(config.WebFeatureRootPath, webFeaturePath);
        var busyKey = StringUtilities.ToKebabCase(StringUtilities.StripSuffix(request.PageName, GeneratorConstants.RazorExtension));

        var routeParams = StringUtilities.ParseRouteParameters(request.Route);
        var queries = request.Queries.Select(ToTemplateModel).ToList();

        plan.AddCreateFile(
            Path.Combine(featureRoot, $"{StringUtilities.StripSuffix(request.PageName, GeneratorConstants.RazorExtension)}.razor"),
            renderer.Render(TemplateNames.WebPage, new
            {
                route = request.Route,
                busy_key = busyKey,
                has_queries = queries.Count > 0,
                route_parameters = routeParams.Select(rp => new RouteParameterModel(rp.Name, rp.Type)).ToList(),
                queries,
            }));

        if (request.CreateImports)
        {
            var applicationFeatureNamespace = $"{config.RootNamespace}.{config.FeatureRoot}.{webFeaturePath.Replace("/", ".")}";
            var webFeatureNamespace = $"{config.Conventions.WebRootNamespace}.{config.FeatureRoot}.{webFeaturePath.Replace("/", ".")}";

            new RazorImportsGenerator(config).AddUsingsToPlan(
                plan,
                request.WebFeaturePath,
                [applicationFeatureNamespace, webFeatureNamespace]);
        }
    }

    private static QueryBindingTemplateModel ToTemplateModel(WebPageQueryBinding binding)
    {
        var variableName = binding.VariableName ?? "_" + StringUtilities.ToCamelCase(StringUtilities.StripSuffix(binding.QueryName, GeneratorConstants.QuerySuffix));
        var queryBaseName = StringUtilities.StripSuffix(binding.QueryName, GeneratorConstants.QuerySuffix);
        var nameWithoutVerb = StripQueryVerbPrefix(queryBaseName);
        var loadMethod = string.Format(GeneratorConstants.LoadMethodPattern, nameWithoutVerb);
        var isList = binding.ResponseShape is ResponseShape.List or ResponseShape.Enumerable;

        return new QueryBindingTemplateModel(
            query_name: binding.QueryName,
            result_type: binding.ResultTypeName,
            variable_name: variableName,
            args: binding.Args,
            is_list: isList,
            has_refresh: binding.HasRefresh,
            refresh_method: loadMethod);
    }

    private static string StripQueryVerbPrefix(string name)
    {
        foreach (var prefix in GeneratorConstants.QueryVerbPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
                return name[prefix.Length..];
        }
        return name;
    }

    private static void Validate(WebPageGenerationRequest request)
    {
        CSharpNameValidator.EnsureFeaturePath(request.WebFeaturePath);
        CSharpNameValidator.EnsureIdentifier(StringUtilities.StripSuffix(request.PageName, GeneratorConstants.RazorExtension), nameof(request.PageName));
        if (string.IsNullOrWhiteSpace(request.Route) || !request.Route.StartsWith('/'))
        {
            throw new ArgumentException("Route must start with '/'.", nameof(request.Route));
        }
    }
}
