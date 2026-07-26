using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Workflows;

public sealed record AddRepositoryScenarioWorkflowRequest(
    string EntityName,
    string EntityNamespace,
    bool CreateEntity,
    string? EntitySubfolder,
    IReadOnlyList<PropertySpec> EntityProperties,
    bool GenerateEntityFactoryMethod,
    bool GenerateEntityEfMapping,
    IReadOnlyList<string> EntityDomainMethods,
    IReadOnlyList<(string DomainName, string EfName)>? EntityEfMappingFields,
    bool AddDependencyInjectionRegistration,
    IReadOnlyList<RepositoryMethodSpec> Methods);

public sealed class AddRepositoryScenarioWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddRepositoryScenarioWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(AddRepositoryScenarioWorkflowRequest request)
    {
        var plan = new GenerationPlan();
        ApplyToPlan(plan, request);
        return plan;
    }

    public void ApplyToPlan(GenerationPlan plan, AddRepositoryScenarioWorkflowRequest request)
    {
        if (request.CreateEntity)
        {
            plan.Merge(_context.CreateAddEntityWorkflow().CreatePlan(new EntityGenerationRequest
            {
                EntityName = request.EntityName,
                Namespace = request.EntityNamespace,
                SubFolder = request.EntitySubfolder,
                Properties = request.EntityProperties,
                GenerateFactoryMethod = request.GenerateEntityFactoryMethod,
                DomainMethods = request.EntityDomainMethods,
                GenerateEfMapping = request.GenerateEntityEfMapping,
                EfMappingFields = request.EntityEfMappingFields,
            }));
        }

        plan.Merge(_context.CreateAddRepositoryWorkflow().CreatePlan(new RepositoryGenerationRequest
        {
            EntityName = request.EntityName,
            EntityNamespace = request.EntityNamespace,
            AddDependencyInjectionRegistration = request.AddDependencyInjectionRegistration,
            Methods = request.Methods,
        }));
    }
}

public sealed record AddCommandScenarioWorkflowRequest(
    AddCommandWorkflowRequest Command,
    IReadOnlyList<AddRepositoryScenarioWorkflowRequest> Repositories);

public sealed class AddCommandScenarioWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddCommandScenarioWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(AddCommandScenarioWorkflowRequest request)
    {
        var plan = new GenerationPlan();
        ApplyToPlan(plan, request);
        return plan;
    }

    public void ApplyToPlan(GenerationPlan plan, AddCommandScenarioWorkflowRequest request)
    {
        var repositoryWorkflow = _context.CreateAddRepositoryScenarioWorkflow();
        foreach (var repository in request.Repositories)
        {
            repositoryWorkflow.ApplyToPlan(plan, repository);
        }

        _context.CreateAddCommandWorkflow().ApplyToPlan(plan, request.Command);
    }
}
