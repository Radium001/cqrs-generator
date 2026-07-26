using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record AddQueryFormState(
    string? FeatureName,
    string? FeaturePath,
    string QueryName,
    QueryDtoSelectionState? DtoSelection,
    IReadOnlyList<PropertySpec>? CustomDtoProperties,
    ResponseShape ResponseShape,
    IReadOnlyList<PropertySpec> Parameters,
    QueryServiceSuggestion? QueryService,
    bool CreateQueryServiceMethod,
    string MethodName,
    bool GenerateHandlerBody,
    bool GenerateQueryServiceBody,
    string? WebFeaturePath)
{
    public AddQueryFormState(
        string? featureName,
        string? featurePath,
        string queryName,
        string? resultTypeName,
        bool createCustomDto,
        IReadOnlyList<PropertySpec>? customDtoProperties,
        ResponseShape responseShape,
        IReadOnlyList<PropertySpec> parameters,
        QueryServiceSuggestion? queryService,
        bool createQueryServiceMethod,
        string methodName,
        bool generateHandlerBody,
        bool generateQueryServiceBody,
        string? webFeaturePath)
        : this(
            featureName,
            featurePath,
            queryName,
            string.IsNullOrWhiteSpace(resultTypeName)
                ? null
                : new QueryDtoSelectionState(
                    resultTypeName,
                    null,
                    null,
                    DtoLocationKind.SharedFeatureDto,
                    null,
                    createCustomDto,
                    IsSelectable: true,
                    SelectionBlockedReason: null),
            customDtoProperties,
            responseShape,
            parameters,
            queryService,
            createQueryServiceMethod,
            methodName,
            generateHandlerBody,
            generateQueryServiceBody,
            webFeaturePath)
    {
    }

    public string? ResultTypeName => DtoSelection?.DtoName;

    public bool CreateCustomDto => DtoSelection?.CreateNewLocalDto == true;
}
