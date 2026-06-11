using System.Collections.ObjectModel;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Session.States;

public sealed class QueryGeneratorState
{
    public string QueryName { get; set; } = string.Empty;

    public ResponseShape ResponseShape { get; set; } = ResponseShape.Single;

    public string FeaturePath { get; set; } = string.Empty;

    public string? CustomDtoName { get; set; }

    public ObservableCollection<PropertySpec> Parameters { get; } = new();

    public bool CreateQueryServiceMethod { get; set; } = true;

    public string MethodName { get; set; } = string.Empty;

    public bool GenerateHandlerBody { get; set; } = true;

    public bool GenerateQueryServiceBody { get; set; } = true;

    public bool UpdateWebImports { get; set; } = true;
}