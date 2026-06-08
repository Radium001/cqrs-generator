namespace CqrsGenerator.Core.Generation;

internal sealed class RouteParameterModel
{
    public string name { get; }
    public string param_type { get; }

    public RouteParameterModel(string name, string paramType)
    {
        this.name = name;
        param_type = paramType;
    }
}

internal sealed class QueryBindingTemplateModel
{
    public string query_name { get; }
    public string result_type { get; }
    public string variable_name { get; }
    public string args { get; }
    public bool is_list { get; }
    public bool has_refresh { get; }
    public string refresh_method { get; }

    public QueryBindingTemplateModel(
        string query_name,
        string result_type,
        string variable_name,
        string args,
        bool is_list,
        bool has_refresh,
        string refresh_method)
    {
        this.query_name = query_name;
        this.result_type = result_type;
        this.variable_name = variable_name;
        this.args = args;
        this.is_list = is_list;
        this.has_refresh = has_refresh;
        this.refresh_method = refresh_method;
    }
}
