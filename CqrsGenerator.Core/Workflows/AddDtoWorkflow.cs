using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Workflows;

public sealed record AddDtoWorkflowRequest(
    string FeaturePath,
    string DtoName,
    IReadOnlyList<PropertySpec> Properties,
    string? WebFeaturePath = null,
    string? Subfolder = null);

public sealed class AddDtoWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddDtoWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(AddDtoWorkflowRequest request)
    {
        var config = _context.Config;
        var plan = _context.CreateDtoGenerator().CreatePlan(new DtoGenerationRequest
        {
            FeaturePath = request.FeaturePath,
            DtoName = request.DtoName,
            Properties = request.Properties,
            Subfolder = request.Subfolder,
        });

        if (!string.IsNullOrWhiteSpace(request.WebFeaturePath))
        {
            var dtoNamespace = StringUtilities.ToNamespace(
                config.RootNamespace,
                config.FeatureRoot,
                StringUtilities.NormalizeFeaturePath(request.FeaturePath),
                config.DtoFolderName);
            plan.Merge(_context.CreateRazorImportsGenerator().AddUsing(request.WebFeaturePath, dtoNamespace));
        }

        return plan;
    }
}
