using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddEntityRequestBuilder : IAddEntityRequestBuilder
{
    private readonly EfEntityPreparationService _efEntityPreparationService;

    public AddEntityRequestBuilder(EfEntityPreparationService efEntityPreparationService)
    {
        _efEntityPreparationService = efEntityPreparationService;
    }

    public AddEntityRequestBuildResult Build(AddEntityFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        try
        {
            var entityName = formState.EntityName.Trim();
            CSharpNameValidator.EnsureIdentifier(entityName, nameof(formState.EntityName));
            var normalizedSubfolder = CSharpNameValidator.NormalizeOptionalRelativePath(formState.Subfolder, nameof(formState.Subfolder));

            foreach (var property in formState.ManualProperties)
            {
                CSharpNameValidator.EnsureIdentifier(property.Name.Trim(), nameof(property.Name));
                if (string.IsNullOrWhiteSpace(property.Type))
                {
                    return AddEntityRequestBuildResult.Failure("Entity property type is required.");
                }
            }

            foreach (var methodName in formState.DomainMethods)
            {
                CSharpNameValidator.EnsureIdentifier(methodName.Trim(), nameof(formState.DomainMethods));
            }

            IReadOnlyList<PropertySpec> finalProperties;
            IReadOnlyList<(string DomainName, string EfName)>? efMappingFields = null;

            if (formState.SourceMode == EntitySourceMode.EfEntity)
            {
                if (formState.SelectedEfEntity is null)
                {
                    return AddEntityRequestBuildResult.Failure("EF entity is not selected.");
                }

                var prepared = _efEntityPreparationService.Prepare(
                    formState.SelectedEfEntity,
                    formState.SelectedEfPropertyNames,
                    formState.RenameEfIdentifierProperties);

                finalProperties = prepared.Properties
                    .Concat(formState.ManualProperties.Select(property => new PropertySpec(property.Type.Trim(), property.Name.Trim())))
                    .ToArray();

                if (formState.GenerateEfMapping)
                {
                    efMappingFields = prepared.EfMappingFields;
                }
            }
            else
            {
                finalProperties = formState.ManualProperties
                    .Select(property => new PropertySpec(property.Type.Trim(), property.Name.Trim()))
                    .ToArray();
            }

            return AddEntityRequestBuildResult.Success(
                new EntityGenerationRequest
                {
                    EntityName = entityName,
                    SubFolder = normalizedSubfolder,
                    Namespace = normalizedSubfolder is null ? GeneratorConstants.DomainEntitiesNamespace : $"{GeneratorConstants.DomainEntitiesNamespace}.{normalizedSubfolder.Replace('/', '.')}",
                    Properties = finalProperties,
                    GenerateFactoryMethod = formState.GenerateFactoryMethod,
                    GenerateEfMapping = formState.GenerateEfMapping,
                    GenerateInterface = formState.GenerateInterface,
                    DomainMethods = formState.DomainMethods.Select(methodName => methodName.Trim()).ToArray(),
                    EfMappingFields = efMappingFields,
                });
        }
        catch (ArgumentException ex)
        {
            return AddEntityRequestBuildResult.Failure(ex.Message);
        }
    }
}
