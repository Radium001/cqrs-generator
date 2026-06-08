namespace CqrsGenerator.Core.Generation;

public static class GeneratorConstants
{
    public static readonly string[] QueryVerbPrefixes = ["Get", "Find", "Fetch", "Search", "List", "Load"];
    public static readonly string[] CommandVerbPrefixes = ["Create", "Update", "Delete"];

    public const string QuerySuffix = "Query";
    public const string CommandSuffix = "Command";
    public const string DtoSuffix = "Dto";
    public const string PageSuffix = "Page";
    public const string AsyncSuffix = "Async";
    public const string HandlerSuffix = "Handler";
    public const string ServiceSuffix = "Service";
    public const string RepositorySuffix = "Repository";
    public const string EntitySuffix = "Entity";
    public const string RazorExtension = ".razor";

    public const string ListWrapperPrefix = "List<";
    public const string EnumerableWrapperPrefix = "IEnumerable<";
    public const string GenericWrapperSuffix = ">";

    public const string QueryServiceInterfacePattern = "I{0}QueryService";
    public const string QueryServiceImplementationPattern = "{0}QueryService";
    public const string RepositoryInterfacePattern = "I{0}Repository";
    public const string RepositoryImplementationPattern = "{0}Repository";
    public const string EntityInterfacePattern = "I{0}Entity";
    public const string QueryClassPattern = "{0}Query";
    public const string HandlerClassPattern = "{0}Handler";
    public const string CommandClassPattern = "{0}Command";
    public const string LoadMethodPattern = "Load{0}Async";

    public static readonly HashSet<string> StandardDependencies = ["ICurrentUserContext", "IUnitOfWork"];

    public const string DomainEntitiesNamespace = "Domain.Entities";
    public const string DomainInterfacesEntitiesNamespace = "Domain.Interfaces.Entities";
    public const string InfrastructureDataEntitiesNamespace = "Infrastructure.Data.Entities";
    public const string InfrastructureDataQueryServicesNamespace = "Infrastructure.Data.QueryServices";

    public const string DefaultStubBody = "throw new NotImplementedException();";

    public const string CancellationTokenType = "CancellationToken";
    public const string CancellationTokenParamName = "ct";

    public const string RepoMethodGetById = "GetByIdAsync";
    public const string RepoMethodAdd = "AddAsync";
    public const string RepoMethodUpdate = "UpdateAsync";
    public const string RepoMethodDelete = "DeleteAsync";
    public const string DefaultIdParamName = "Id";
    public const string DefaultEntityParamName = "entity";
}
