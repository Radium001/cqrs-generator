using CqrsGenerator.Gui.Models;

namespace CqrsGenerator.Gui.Services;

public interface IAddEntityRequestBuilder
{
    AddEntityRequestBuildResult Build(AddEntityFormState formState);
}
