using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Models;

public sealed record NewRepositoryDraft(
    string EntityName,
    string EntityNamespace,
    bool CreateEntity,
    NewEntityDraft? CustomEntity,
    IReadOnlyList<RepositoryMethodSpec> Methods,
    bool AddDependencyInjectionRegistration)
{
    public string InterfaceName => GenerationNaming.GetRepositoryInterfaceName(EntityName);
}
