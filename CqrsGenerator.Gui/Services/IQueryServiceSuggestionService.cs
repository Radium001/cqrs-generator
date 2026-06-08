using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IQueryServiceSuggestionService
{
    QueryServiceSuggestion? Suggest(ProjectModel? projectModel, string? featureName, string? featurePath);
}
