using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class AddQueryRequestBuilder : IAddQueryRequestBuilder
{
    public AddQueryRequestBuildResult Build(AddQueryFormState formState)
    {
        ArgumentNullException.ThrowIfNull(formState);

        try
        {
            if (string.IsNullOrWhiteSpace(formState.FeaturePath))
            {
                return AddQueryRequestBuildResult.Failure("Feature is not selected.");
            }

            CSharpNameValidator.EnsureFeaturePath(formState.FeaturePath);

            var queryName = formState.QueryName.Trim();
            CSharpNameValidator.EnsureIdentifier(StringUtilities.StripSuffix(queryName, "Query"), nameof(formState.QueryName));

            var dtoSelection = formState.DtoSelection;
            if (dtoSelection is null || string.IsNullOrWhiteSpace(dtoSelection.DtoName))
            {
                return AddQueryRequestBuildResult.Failure("Result type is not configured.");
            }

            var dtoTypeName = dtoSelection.DtoName.Trim();
            CSharpNameValidator.EnsureTypeName(dtoTypeName, nameof(formState.DtoSelection));

            foreach (var parameter in formState.Parameters)
            {
                CSharpNameValidator.EnsureIdentifier(parameter.Name, nameof(parameter.Name));
            }

            if (!dtoSelection.IsSelectable)
            {
                return AddQueryRequestBuildResult.Failure(dtoSelection.SelectionBlockedReason ?? "Selected DTO cannot be used.");
            }

            QueryServiceMethodWorkflowRequest? queryServiceRequest = null;
            if (formState.CreateQueryServiceMethod)
            {
                if (formState.QueryService is null)
                {
                    return AddQueryRequestBuildResult.Failure("Query service is not available.");
                }

                if (formState.QueryService.Mode == QueryServiceSuggestionMode.Blocked)
                {
                    return AddQueryRequestBuildResult.Failure("Query service implementation could not be resolved automatically. Normalize the existing implementation placement before adding methods through this flow.");
                }

                if (string.IsNullOrWhiteSpace(formState.MethodName))
                {
                    return AddQueryRequestBuildResult.Failure("Method name is not configured.");
                }

                var methodName = formState.MethodName.Trim();
                CSharpNameValidator.EnsureIdentifier(StringUtilities.StripSuffix(methodName, "Async"), nameof(formState.MethodName));

                queryServiceRequest = new QueryServiceMethodWorkflowRequest(
                    formState.QueryService.InterfaceName,
                    formState.QueryService.ImplementationName,
                    formState.QueryService.InterfacePath,
                    formState.QueryService.ImplementationPath,
                    CreateNew: formState.QueryService.Mode == QueryServiceSuggestionMode.CreateNew,
                    AddDependencyInjectionRegistration: formState.QueryService.Mode == QueryServiceSuggestionMode.CreateNew,
                    AddMethod: true,
                    GenerateImplementationBody: formState.GenerateQueryServiceBody,
                    MethodName: methodName,
                    ReturnType: $"Task<{GenerationNaming.GetResponseType(dtoTypeName, formState.ResponseShape)}>",
                    DtoTypeName: dtoTypeName);
            }

            QueryDtoSelection requestDtoSelection;
            if (dtoSelection.CreateNewLocalDto)
            {
                requestDtoSelection = new CreateLocalQueryDtoSelection(
                    dtoTypeName,
                    formState.CustomDtoProperties ?? [],
                    StringUtilities.StripSuffix(queryName, GeneratorConstants.QuerySuffix));
            }
            else if (dtoSelection.LocationKind == DtoLocationKind.LocalQueryDto)
            {
                if (string.IsNullOrWhiteSpace(dtoSelection.DtoPath)
                    || string.IsNullOrWhiteSpace(dtoSelection.DtoNamespace)
                    || string.IsNullOrWhiteSpace(dtoSelection.OwnerQueryName))
                {
                    return AddQueryRequestBuildResult.Failure("Selected local DTO is missing source metadata.");
                }

                requestDtoSelection = new PromoteLocalQueryDtoSelection(
                    dtoTypeName,
                    dtoSelection.DtoPath,
                    dtoSelection.DtoNamespace,
                    dtoSelection.OwnerQueryName,
                    formState.FeaturePath);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dtoSelection.DtoNamespace) && string.IsNullOrWhiteSpace(dtoSelection.DtoPath))
                {
                    requestDtoSelection = new UseSharedFeatureDtoSelection(
                        dtoTypeName,
                        string.Empty,
                        string.Empty);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(dtoSelection.DtoNamespace) || string.IsNullOrWhiteSpace(dtoSelection.DtoPath))
                    {
                        return AddQueryRequestBuildResult.Failure("Selected shared DTO is missing metadata.");
                    }

                    requestDtoSelection = new UseSharedFeatureDtoSelection(
                        dtoTypeName,
                        dtoSelection.DtoNamespace,
                        dtoSelection.DtoPath);
                }
            }

            return AddQueryRequestBuildResult.Success(
                new AddQueryWorkflowRequest(
                    formState.FeaturePath,
                    queryName,
                    requestDtoSelection,
                    formState.ResponseShape,
                    formState.Parameters,
                    formState.GenerateHandlerBody,
                    formState.GenerateQueryServiceBody,
                    queryServiceRequest,
                    formState.UpdateWebImports));
        }
        catch (ArgumentException ex)
        {
            return AddQueryRequestBuildResult.Failure(ex.Message);
        }
    }
}
