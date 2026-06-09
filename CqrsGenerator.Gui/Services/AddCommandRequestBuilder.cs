using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddCommandRequestBuilder : IAddCommandRequestBuilder
{
    public AddCommandRequestBuildResult Build(AddCommandFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        try
        {
            if (string.IsNullOrWhiteSpace(formState.FeaturePath))
            {
                return AddCommandRequestBuildResult.Failure("Feature is not selected.");
            }

            CSharpNameValidator.EnsureFeaturePath(formState.FeaturePath);

            var commandName = formState.CommandName.Trim();
            CSharpNameValidator.EnsureIdentifier(commandName, nameof(formState.CommandName));

            string? responseType = null;
            if (formState.ResponseType is not null)
            {
                responseType = formState.ResponseType.Trim();
                CSharpNameValidator.EnsureIdentifier(responseType, nameof(formState.ResponseType));
            }

            foreach (var parameter in formState.Properties)
            {
                CSharpNameValidator.EnsureIdentifier(parameter.Name, nameof(parameter.Name));
            }

            var repositoryRequests = Array.Empty<AddRepositoryScenarioWorkflowRequest>();

            return AddCommandRequestBuildResult.Success(
                new AddCommandScenarioWorkflowRequest(
                    new AddCommandWorkflowRequest(
                        formState.FeaturePath,
                        commandName,
                        responseType,
                        formState.Properties,
                        formState.Dependencies,
                        formState.UpdateWebImports),
                    repositoryRequests));
        }
        catch (ArgumentException ex)
        {
            return AddCommandRequestBuildResult.Failure(ex.Message);
        }
    }
}
