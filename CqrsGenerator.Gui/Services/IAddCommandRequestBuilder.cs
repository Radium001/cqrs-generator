using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddCommandRequestBuilder
{
    AddCommandRequestBuildResult Build(AddCommandFormState formState);
}
