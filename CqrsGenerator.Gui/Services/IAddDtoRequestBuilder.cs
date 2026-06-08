using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddDtoRequestBuilder
{
    AddDtoRequestBuildResult Build(AddDtoFormState formState);
}
