using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddQueryRequestBuilder
{
    AddQueryRequestBuildResult Build(AddQueryFormState formState);
}
