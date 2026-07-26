using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddDtoRequestBuilder : IAddDtoRequestBuilder
{
    public AddDtoRequestBuildResult Build(AddDtoFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        try
        {
            if (string.IsNullOrWhiteSpace(formState.FeaturePath))
                return AddDtoRequestBuildResult.Failure("Feature is not selected.");

            CSharpNameValidator.EnsureFeaturePath(formState.FeaturePath);

            var dtoName = formState.DtoName.Trim();
            CSharpNameValidator.EnsureIdentifier(dtoName, nameof(formState.DtoName));
            var normalizedSubfolder = CSharpNameValidator.NormalizeOptionalRelativePath(formState.Subfolder, nameof(formState.Subfolder));

            foreach (var parameter in formState.Properties)
                CSharpNameValidator.EnsureIdentifier(parameter.Name, nameof(parameter.Name));

            return AddDtoRequestBuildResult.Success(
                new AddDtoWorkflowRequest(
                    formState.FeaturePath,
                    dtoName,
                    formState.Properties,
                    formState.WebFeaturePath,
                    normalizedSubfolder));
        }
        catch (ArgumentException ex)
        {
            return AddDtoRequestBuildResult.Failure(ex.Message);
        }
    }
}
