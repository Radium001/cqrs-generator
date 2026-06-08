using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Core.Workflows;

public sealed record QueryServiceMethodWorkflowRequest(
    string InterfaceName,
    string ImplementationName,
    string? InterfacePath,
    string? ImplementationPath,
    bool CreateNew,
    bool AddDependencyInjectionRegistration,
    bool AddMethod,
    bool GenerateImplementationBody,
    string MethodName,
    string ReturnType,
    string? DtoTypeName);

public sealed record AddQueryWorkflowRequest(
    string FeaturePath,
    string QueryName,
    QueryDtoSelection DtoSelection,
    ResponseShape ResponseShape,
    IReadOnlyList<PropertySpec> Properties,
    bool GenerateHandlerBody,
    bool GenerateQueryServiceBody,
    QueryServiceMethodWorkflowRequest? QueryService,
    bool UpdateWebImports = true)
{
    public string DtoName => DtoSelection.DtoName;

    public bool CreateDto => DtoSelection is CreateLocalQueryDtoSelection;

    public IReadOnlyList<PropertySpec> CustomDtoProperties =>
        DtoSelection is CreateLocalQueryDtoSelection createLocal ? createLocal.Properties : [];

    public AddQueryWorkflowRequest(
        string featurePath,
        string queryName,
        string dtoName,
        bool createDto,
        IReadOnlyList<PropertySpec> customDtoProperties,
        ResponseShape responseShape,
        IReadOnlyList<PropertySpec> properties,
        bool generateHandlerBody,
        bool generateQueryServiceBody,
        QueryServiceMethodWorkflowRequest? queryService,
        bool updateWebImports = true)
        : this(
            featurePath,
            queryName,
            createDto
                ? new CreateLocalQueryDtoSelection(dtoName, customDtoProperties, StringUtilities.StripSuffix(queryName, GeneratorConstants.QuerySuffix))
                : new UseSharedFeatureDtoSelection(dtoName, string.Empty, string.Empty),
            responseShape,
            properties,
            generateHandlerBody,
            generateQueryServiceBody,
            queryService,
            updateWebImports)
    {
    }
}

public sealed class AddQueryWorkflow
{
    private readonly CoreWorkflowContext _context;

    internal AddQueryWorkflow(CoreWorkflowContext context)
    {
        _context = context;
    }

    public GenerationPlan CreatePlan(AddQueryWorkflowRequest request)
    {
        var plan = new GenerationPlan();
        ApplyToPlan(plan, request);
        return plan;
    }

    public void ApplyToPlan(GenerationPlan plan, AddQueryWorkflowRequest request)
    {
        var dtoNamespace = ResolveDtoSelection(plan, request);

        plan.Merge(_context.CreateQueryGenerator().CreatePlan(new QueryGenerationRequest
        {
            FeaturePath = request.FeaturePath,
            QueryName = request.QueryName,
            DtoName = request.DtoSelection.DtoName,
            DtoNamespace = dtoNamespace,
            ResponseShape = request.ResponseShape,
            Properties = request.Properties,
            ServiceInterfaceName = request.QueryService?.InterfaceName,
            ServiceMethodName = request.QueryService?.MethodName,
            GenerateHandlerBody = request.GenerateHandlerBody,
        }));

        if (request.QueryService is { AddMethod: true } service)
        {
            var config = _context.Config;
            var generator = _context.CreateQueryServiceGenerator();
            if (service.CreateNew)
            {
                plan.Merge(generator.CreateServicePlan(new QueryServiceGenerationRequest
                {
                    FeaturePath = request.FeaturePath,
                    InterfaceName = service.InterfaceName,
                    ImplementationName = service.ImplementationName,
                    ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(config, request.FeaturePath, service.ImplementationName),
                    ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(config, request.FeaturePath),
                    AddDependencyInjectionRegistration = service.AddDependencyInjectionRegistration,
                    InitialReturnType = service.ReturnType,
                    InitialMethodName = service.MethodName,
                    InitialParameters = request.Properties,
                    GenerateImplementationBody = request.GenerateQueryServiceBody,
                    DtoTypeName = request.DtoSelection.DtoName,
                }));
            }
            else
            {
                generator.AddMethodToPlan(plan, new QueryServiceMethodGenerationRequest
                {
                    InterfacePath = service.InterfacePath ?? Path.Combine(config.ApplicationFeatureRootPath, request.FeaturePath, config.InterfacesFolderName, $"{service.InterfaceName}.cs"),
                    ImplementationPath = service.ImplementationPath ?? GenerationNaming.GetQueryServiceImplementationPath(config, request.FeaturePath, service.ImplementationName),
                    InterfaceName = service.InterfaceName,
                    ImplementationName = service.ImplementationName,
                    ReturnType = service.ReturnType,
                    MethodName = service.MethodName,
                    Parameters = request.Properties,
                    GenerateImplementationBody = request.GenerateQueryServiceBody,
                    DtoTypeName = request.DtoSelection.DtoName,
                });
            }
        }

        if (request.UpdateWebImports)
        {
            var config = _context.Config;
            var imports = _context.CreateRazorImportsGenerator();
            var queryNamespace = GenerationNaming.ToQueryNamespace(config, request.FeaturePath, request.QueryName);

            imports.AddUsingsToPlan(plan, request.FeaturePath, [queryNamespace, dtoNamespace]);
        }
    }

    private string ResolveDtoSelection(GenerationPlan plan, AddQueryWorkflowRequest request)
    {
        return request.DtoSelection switch
        {
            CreateLocalQueryDtoSelection createLocal => CreateLocalDto(plan, request.FeaturePath, request.QueryName, createLocal),
            UseSharedFeatureDtoSelection useShared => string.IsNullOrWhiteSpace(useShared.DtoNamespace)
                ? GenerationNaming.ToDtoNamespace(_context.Config, request.FeaturePath)
                : useShared.DtoNamespace,
            PromoteLocalQueryDtoSelection promoteLocal => PromoteLocalDto(plan, request.FeaturePath, promoteLocal),
            _ => throw new InvalidOperationException($"Unsupported DTO selection: {request.DtoSelection.GetType().Name}"),
        };
    }

    private string CreateLocalDto(GenerationPlan plan, string featurePath, string queryName, CreateLocalQueryDtoSelection selection)
    {
        plan.Merge(_context.CreateLocalQueryDtoGenerator().CreatePlan(new LocalQueryDtoGenerationRequest
        {
            FeaturePath = featurePath,
            QueryName = queryName,
            DtoName = selection.DtoName,
            Properties = selection.Properties,
        }));

        return GenerationNaming.ToLocalQueryDtoNamespace(_context.Config, featurePath, queryName);
    }

    private string PromoteLocalDto(GenerationPlan plan, string featurePath, PromoteLocalQueryDtoSelection selection)
    {
        if (!string.Equals(featurePath, selection.TargetFeaturePath, StringComparison.OrdinalIgnoreCase))
        {
            plan.AddConflict(selection.SourcePath, "Cross-feature DTO promotion is not supported.");
            return GenerationNaming.ToDtoNamespace(_context.Config, featurePath);
        }

        var targetNamespace = GenerationNaming.ToDtoNamespace(_context.Config, featurePath);
        var targetPath = Path.Combine(
            _context.Config.ApplicationFeatureRootPath,
            featurePath.Replace('/', Path.DirectorySeparatorChar),
            _context.Config.DtoFolderName,
            $"{selection.DtoName}.cs");

        if (File.Exists(targetPath))
        {
            plan.AddConflict(targetPath, $"Shared DTO '{selection.DtoName}' already exists in the feature.");
            return targetNamespace;
        }

        if (!File.Exists(selection.SourcePath))
        {
            plan.AddConflict(selection.SourcePath, "Source local DTO file was not found.");
            return targetNamespace;
        }

        var sourceContent = File.ReadAllText(selection.SourcePath);
        var rewrittenContent = RewriteNamespaceOrConflict(plan, selection.SourcePath, sourceContent, targetNamespace);
        if (rewrittenContent is null)
        {
            return targetNamespace;
        }

        plan.AddCreateFile(targetPath, rewrittenContent);
        plan.AddDeleteFile(selection.SourcePath);

        var ownerDirectory = Path.GetDirectoryName(selection.SourcePath);
        if (!string.IsNullOrWhiteSpace(ownerDirectory) && Directory.Exists(ownerDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(ownerDirectory, "*.cs", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFullPath(file).Equals(Path.GetFullPath(selection.SourcePath), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    plan.TransformFile(file, content =>
                        _context.Editor.AddUsingIfTypeReferenced(content, targetNamespace, selection.DtoName));
                }
                catch (InvalidOperationException ex)
                {
                    plan.AddConflict(file, ex.Message);
                }
            }
        }

        return targetNamespace;
    }

    private string? RewriteNamespaceOrConflict(GenerationPlan plan, string path, string source, string targetNamespace)
    {
        try
        {
            return _context.Editor.ReplaceNamespace(source, targetNamespace);
        }
        catch (InvalidOperationException ex)
        {
            plan.AddConflict(path, ex.Message);
            return null;
        }
    }
}
