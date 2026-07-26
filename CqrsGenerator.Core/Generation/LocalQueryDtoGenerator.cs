using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class LocalQueryDtoGenerator(GeneratorConfig config, ScribanTemplateRenderer renderer)
{
    public GenerationPlan CreatePlan(LocalQueryDtoGenerationRequest request)
    {
        Validate(request);

        var plan = new GenerationPlan();
        var featurePath = StringUtilities.NormalizeFeaturePath(request.FeaturePath);
        var queryBaseName = StringUtilities.StripSuffix(request.QueryName, GeneratorConstants.QuerySuffix);
        var dtoDirectory = Path.Combine(GetFeatureRoot(featurePath), config.QueriesFolderName, queryBaseName);
        var dtoNamespace = GenerationNaming.ToLocalQueryDtoNamespace(config, featurePath, request.QueryName);

        plan.AddCreateFile(
            Path.Combine(dtoDirectory, $"{request.DtoName}.cs"),
            renderer.Render(TemplateNames.Dto, new
            {
                feature_path = featurePath,
                dto_type = request.DtoName,
                @namespace = dtoNamespace,
                usings_block = CSharpTypeMetadataResolver.CreateUsingsBlock(request.Properties.Select(property => property.Type)),
                has_properties = request.Properties.Count > 0,
                properties_block = string.Join("\n", request.Properties.Select(property => $"        public {property.Type} {property.Name} {{ get; set; }}")),
            }));

        return plan;
    }

    private static void Validate(LocalQueryDtoGenerationRequest request)
    {
        CSharpNameValidator.EnsureFeaturePath(request.FeaturePath);
        CSharpNameValidator.EnsureIdentifier(StringUtilities.StripSuffix(request.QueryName, GeneratorConstants.QuerySuffix), nameof(request.QueryName));
        CSharpNameValidator.EnsureIdentifier(request.DtoName, nameof(request.DtoName));

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
}
