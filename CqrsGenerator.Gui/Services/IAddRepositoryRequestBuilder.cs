using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddRepositoryRequestBuilder
{
    AddRepositoryRequestBuildResult Build(AddRepositoryFormState formState);
}
