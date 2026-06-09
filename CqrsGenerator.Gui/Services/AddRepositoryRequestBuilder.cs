using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddRepositoryRequestBuilder : IAddRepositoryRequestBuilder
{
    public AddRepositoryRequestBuildResult Build(AddRepositoryFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        try
        {
            var customEntity = formState.CustomEntity;
            var normalizedSubfolder = null as string;
            var entityName = formState.ExistingEntityName;
            var entityNamespace = formState.ExistingEntityNamespace;

            if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(entityNamespace))
            {
                return AddRepositoryRequestBuildResult.Failure("Entity is not configured.");
            }

            CSharpNameValidator.EnsureIdentifier(entityName.Trim(), nameof(formState.ExistingEntityName));

            var presetMethods = formState.SelectedMethodPresetKeys
                .Select(key => RepositoryMethodCatalog.Create(key, entityName.Trim()))
                .ToList();

            var customMethods = new List<RepositoryMethodSpec>();
            foreach (var method in formState.CustomMethods)
            {
                CSharpNameValidator.EnsureIdentifier(method.Name.Trim(), nameof(method.Name));
                if (string.IsNullOrWhiteSpace(method.ReturnType))
                {
                    return AddRepositoryRequestBuildResult.Failure("Repository method return type is required.");
                }

                foreach (var parameter in method.Parameters)
                {
                    CSharpNameValidator.EnsureIdentifier(parameter.Name.Trim(), nameof(parameter.Name));
                }

                customMethods.Add(new RepositoryMethodSpec(
                    method.Name.Trim(),
                    method.ReturnType.Trim(),
                    method.Parameters.Select(parameter => new PropertySpec(parameter.Type.Trim(), parameter.Name.Trim())).ToArray()));
            }

            if (customEntity is not null)
            {
                foreach (var property in Enumerable.Empty<PropertySpec>())
                {
                    CSharpNameValidator.EnsureIdentifier(property.Name.Trim(), nameof(property.Name));
                }
            }

            return AddRepositoryRequestBuildResult.Success(
                new AddRepositoryScenarioWorkflowRequest(
                    entityName.Trim(),
                    entityNamespace.Trim(),
                    CreateEntity: false,
                    null,
                    [],
                    false,
                    false,
                    false,
                    [],
                    null,
                    formState.AddDependencyInjectionRegistration,
                    presetMethods.Concat(customMethods).ToArray()));
        }
        catch (ArgumentException ex)
        {
            return AddRepositoryRequestBuildResult.Failure(ex.Message);
        }
    }
}
