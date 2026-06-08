namespace CqrsGenerator.Core.Templates;

public static class TemplateNames
{
    public const string Query = "Application/Features/{{ feature_path }}/Queries/{{ operation_name }}/{{ query_type }}.cs.sbn";
    public const string Handler = "Application/Features/{{ feature_path }}/Queries/{{ operation_name }}/{{ handler_type }}.cs.sbn";
    public const string Dto = "Application/Features/{{ feature_path }}/DTOs/{{ dto_type }}.cs.sbn";
    public const string QueryServiceInterface = "Application/Features/{{ feature_path }}/Interfaces/{{ query_service_interface }}.cs.sbn";
    public const string QueryServiceImplementation = "Infrastructure/Data/QueryServices/{{ query_service_implementation }}.cs.sbn";
    public const string Command = "Application/Features/{{ feature_path }}/Commands/{{ operation_name }}/{{ command_type }}.cs.sbn";
    public const string CommandHandler = "Application/Features/{{ feature_path }}/Commands/{{ operation_name }}/{{ handler_type }}.cs.sbn";
    public const string WebPage = "Web/Features/{{ web_feature_path }}/{{ page_name }}.razor.sbn";
    public const string WebImports = "Web/Features/{{ web_feature_path }}/_Imports.razor.sbn";
    public const string RepositoryInterface = "Application/Common/Interfaces/Repositories/{{ repository_interface }}.cs.sbn";
    public const string RepositoryImplementation = "Infrastructure/Data/Repositories/{{ repository_implementation }}.cs.sbn";
    public const string Entity = "Domain/Entities/{{ entity_name }}/{{ entity_name }}.cs.sbn";
    public const string EntityInterface = "Domain/Interfaces/Entities/{{ entity_interface }}.cs.sbn";
    public const string EfMapping = "Infrastructure/Data/EntitiesMapping/{{ entity_name }}.cs.sbn";
}
