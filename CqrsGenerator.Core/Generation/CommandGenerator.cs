using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class CommandGenerator(GeneratorConfig config, ScribanTemplateRenderer renderer)
{
    public GenerationPlan CreatePlan(CommandGenerationRequest request)
    {
        Validate(request);

        var plan = new GenerationPlan();
        var featurePath = StringUtilities.NormalizeFeaturePath(request.FeaturePath);
        var commandBaseName = StringUtilities.StripSuffix(request.CommandName, GeneratorConstants.CommandSuffix);
        var commandType = string.Format(GeneratorConstants.CommandClassPattern, commandBaseName);
        var handlerType = string.Format(GeneratorConstants.HandlerClassPattern, commandBaseName);
        var commandDirectory = Path.Combine(GetFeatureRoot(featurePath), config.CommandsFolderName, commandBaseName);
        var commandNamespace = StringUtilities.ToNamespace(config.RootNamespace, config.FeatureRoot, featurePath, config.CommandsFolderName, commandBaseName);
        plan.AddCreateFile(
            Path.Combine(commandDirectory, $"{commandType}.cs"),
            renderer.Render(TemplateNames.Command, new
            {
                feature_path = featurePath,
                operation_name = commandBaseName,
                command_type = commandType,
                @namespace = commandNamespace,
                command_interface = "ICommand",
                usings_block = ComputeUsingsBlock(request.Properties, []),
                has_properties = request.Properties.Count > 0,
                constructor_parameters = string.Join(", ", request.Properties.Select(ToConstructorParameter)),
                constructor_assignments = string.Join("", request.Properties.Select(property => $"            {property.Name} = {StringUtilities.ToCamelCase(property.Name)};\n")),
                properties_block = string.Join("\n\n", request.Properties.Select(property => $"        public {property.Type} {property.Name} {{ get; }}")),
            }));

        var hasDependencies = request.Dependencies.Count > 0;
        var dependenciesFields = string.Join("\n", request.Dependencies.Select(d =>
            $"        private readonly {d.Type} _{StringUtilities.ToCamelCase(d.Name)};"));
        var dependenciesParams = string.Join(", ", request.Dependencies.Select(d =>
            $"{d.Type} {StringUtilities.ToCamelCase(d.Name)}"));
        var dependenciesAssignments = string.Join("\n", request.Dependencies.Select(d =>
            $"            _{StringUtilities.ToCamelCase(d.Name)} = {StringUtilities.ToCamelCase(d.Name)};\n"));
        var handlerBody = CommandHandlerBodyBuilder.Build(request, commandBaseName);
        if (handlerBody.Warning is not null)
        {
            plan.AddWarning(handlerBody.Warning);
        }

        plan.AddCreateFile(
            Path.Combine(commandDirectory, $"{handlerType}.cs"),
            renderer.Render(TemplateNames.CommandHandler, new
            {
                feature_path = featurePath,
                operation_name = commandBaseName,
                command_type = commandType,
                handler_type = handlerType,
                @namespace = commandNamespace,
                usings_block = ComputeUsingsBlock([], request.Dependencies, handlerBody.RequiredNamespaces),
                has_dependencies = hasDependencies,
                dependencies_fields = dependenciesFields,
                dependencies_params = dependenciesParams,
                dependencies_assignments = dependenciesAssignments,
                is_async = handlerBody.IsAsync,
                handler_body = handlerBody.Body,
            }));

        return plan;
    }

    private static void Validate(CommandGenerationRequest request)
    {
        CSharpNameValidator.EnsureFeaturePath(request.FeaturePath);
        CSharpNameValidator.EnsureIdentifier(StringUtilities.StripSuffix(request.CommandName, GeneratorConstants.CommandSuffix), nameof(request.CommandName));

        foreach (var property in request.Properties)
        {
            CSharpNameValidator.EnsureIdentifier(property.Name, nameof(property.Name));
            if (string.IsNullOrWhiteSpace(property.Type))
            {
                throw new ArgumentException("Property type is required.", nameof(property.Type));
            }
            CSharpTypeMetadataResolver.EnsureValid(property.Type, nameof(property.Type));
        }
    }

    private string GetFeatureRoot(string featurePath) =>
        Path.Combine(config.ApplicationFeatureRootPath, featurePath.Replace('/', Path.DirectorySeparatorChar));

    private static string ToConstructorParameter(PropertySpec property) =>
        $"{property.Type} {StringUtilities.ToCamelCase(property.Name)}";

    private static string ComputeUsingsBlock(
        IReadOnlyList<PropertySpec> properties,
        IReadOnlyList<CommandHandlerDependency> dependencies,
        IReadOnlyList<string>? requiredNamespaces = null)
    {
        var usings = new HashSet<string>();
        foreach (var @namespace in CSharpTypeMetadataResolver.GetNamespaces(properties.Select(property => property.Type)))
        {
            usings.Add($"using {@namespace};");
        }
        foreach (var dep in dependencies)
        {
            if (dep.Type.EndsWith("Repository"))
                usings.Add("using Application.Common.Interfaces.Repositories;");
            else if (GeneratorConstants.StandardDependencies.Contains(dep.Type))
                usings.Add("using Application.Common.Interfaces;");
        }
        foreach (var @namespace in requiredNamespaces ?? [])
        {
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                usings.Add($"using {@namespace};");
            }
        }

        return usings.Count == 0 ? "" : string.Join("\n", usings.OrderBy(value => value, StringComparer.Ordinal));
    }
}
