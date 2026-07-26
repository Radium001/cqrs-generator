using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class DtoGenerator(GeneratorConfig config, ScribanTemplateRenderer renderer)
{
    public GenerationPlan CreatePlan(DtoGenerationRequest request)
    {
        Validate(request);

        var plan = new GenerationPlan();
        var featurePath = StringUtilities.NormalizeFeaturePath(request.FeaturePath);
        var normalizedSubfolder = CSharpNameValidator.NormalizeOptionalRelativePath(request.Subfolder, nameof(request.Subfolder));
        var dtoDirectory = string.IsNullOrWhiteSpace(normalizedSubfolder)
            ? Path.Combine(GetFeatureRoot(featurePath), config.DtoFolderName)
            : Path.Combine(GetFeatureRoot(featurePath), config.DtoFolderName, normalizedSubfolder.Replace('/', Path.DirectorySeparatorChar));
        var dtoNamespace = StringUtilities.ToNamespace(
            config.RootNamespace,
            config.FeatureRoot,
            featurePath,
            config.DtoFolderName);

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

    private static void Validate(DtoGenerationRequest request)
    {
        CSharpNameValidator.EnsureFeaturePath(request.FeaturePath);
        CSharpNameValidator.EnsureIdentifier(request.DtoName, nameof(request.DtoName));
        CSharpNameValidator.NormalizeOptionalRelativePath(request.Subfolder, nameof(request.Subfolder));

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
