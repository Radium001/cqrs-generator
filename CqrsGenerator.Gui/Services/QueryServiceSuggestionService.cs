using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public sealed class QueryServiceSuggestionService : IQueryServiceSuggestionService
{
    public QueryServiceSuggestion? Suggest(ProjectModel? projectModel, string? featureName, string? featurePath)
    {
        if (projectModel is null || string.IsNullOrWhiteSpace(featureName) || string.IsNullOrWhiteSpace(featurePath))
        {
            return null;
        }

        var expectedInterfaceName = GenerationNaming.GetQueryServiceInterfaceName(featureName);
        var expectedImplementationName = GenerationNaming.GetQueryServiceImplementationName(featureName);

        var discovered = projectModel.QueryServices.FirstOrDefault(service =>
            string.Equals(service.FeaturePath, featurePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(service.InterfaceName, expectedInterfaceName, StringComparison.Ordinal));

        if (discovered is not null)
        {
            var mode = discovered.ImplementationPlacement switch
            {
                QueryServiceImplementationPlacement.CanonicalFeaturePath => QueryServiceSuggestionMode.UpdateExisting,
                QueryServiceImplementationPlacement.NonCanonical => QueryServiceSuggestionMode.UpdateExisting,
                QueryServiceImplementationPlacement.Missing => QueryServiceSuggestionMode.Blocked,
                QueryServiceImplementationPlacement.Ambiguous => QueryServiceSuggestionMode.Blocked,
                _ => QueryServiceSuggestionMode.Blocked,
            };

            return new QueryServiceSuggestion(
                discovered.InterfaceName,
                discovered.ImplementationName ?? expectedImplementationName,
                discovered.InterfacePath,
                discovered.ImplementationPath,
                mode,
                Status: Models.AutoItemStatus.Modified);
        }

        return new QueryServiceSuggestion(
            expectedInterfaceName,
            expectedImplementationName,
            InterfacePath: null,
            ImplementationPath: null,
            QueryServiceSuggestionMode.CreateNew,
            Status: Models.AutoItemStatus.Created);
    }
}
