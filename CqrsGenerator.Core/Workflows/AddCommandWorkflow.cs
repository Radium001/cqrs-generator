using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Workflows;

public sealed record AddCommandWorkflowRequest(
    string FeaturePath,
    string CommandName,
    IReadOnlyList<PropertySpec> Properties,
    IReadOnlyList<CommandHandlerDependency> Dependencies,
    string? WebFeaturePath = null)
{
    public bool GenerateHandlerBody { get; init; } = true;

    public CommandHandlerScaffoldContext? HandlerScaffoldContext { get; init; }
}

public sealed class AddCommandWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddCommandWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(AddCommandWorkflowRequest request)
    {
        var plan = new GenerationPlan();
        ApplyToPlan(plan, request);
        return plan;
    }

    public void ApplyToPlan(GenerationPlan plan, AddCommandWorkflowRequest request)
    {
        plan.Merge(_context.CreateCommandGenerator().CreatePlan(new CommandGenerationRequest
        {
            FeaturePath = request.FeaturePath,
            CommandName = request.CommandName,
            Properties = request.Properties,
            Dependencies = request.Dependencies,
            GenerateHandlerBody = request.GenerateHandlerBody,
            HandlerScaffoldContext = request.HandlerScaffoldContext,
        }));

        if (!string.IsNullOrWhiteSpace(request.WebFeaturePath))
        {
            var config = _context.Config;
            var commandNamespace = GenerationNaming.ToCommandNamespace(config, request.FeaturePath, request.CommandName);
            _context.CreateRazorImportsGenerator().AddUsingsToPlan(plan, request.WebFeaturePath, [commandNamespace]);
        }
    }
}
