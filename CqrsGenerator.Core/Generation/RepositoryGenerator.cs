using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Editing;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class RepositoryGenerator(
    GeneratorConfig config,
    ScribanTemplateRenderer renderer,
    CSharpSyntaxEditor editor)
{
    public GenerationPlan CreatePlan(RepositoryGenerationRequest request)
    {
        Validate(request);

        var plan = new GenerationPlan();
        var interfaceName = GenerationNaming.GetRepositoryInterfaceName(request.EntityName);
        var implementationName = GenerationNaming.GetRepositoryImplementationName(request.EntityName);
        var interfaceNamespace = request.Namespace ?? $"{config.RootNamespace}.Common.Interfaces.Repositories";
        var implementationNamespace = $"{config.Conventions.InfrastructureRootNamespace}.Data.Repositories";

        var methods = request.Methods.Count > 0
            ? request.Methods
            :
            [
                new(GeneratorConstants.RepoMethodGetById, $"Task<{request.EntityName}>", [new("int", GeneratorConstants.DefaultIdParamName)]),
                new(GeneratorConstants.RepoMethodAdd, "Task", [new(request.EntityName, GeneratorConstants.DefaultEntityParamName)]),
                new(GeneratorConstants.RepoMethodUpdate, "Task", [new(request.EntityName, GeneratorConstants.DefaultEntityParamName)]),
            ];

        var baseMethods = new HashSet<string> { GeneratorConstants.RepoMethodGetById, GeneratorConstants.RepoMethodAdd, GeneratorConstants.RepoMethodUpdate, GeneratorConstants.RepoMethodDelete };
        var interfaceMethods = string.Join("\n", methods.Select(m => GenerateInterfaceMethod(m)));
        var implMethods = string.Join("\n\n", methods
            .Where(m => !baseMethods.Contains(m.Name))
            .Select(m => GenerateImplMethod(request.EntityName, m)));

        var entityNamespace = request.EntityNamespace ?? GeneratorConstants.DomainEntitiesNamespace;

        plan.AddCreateFile(
            Path.Combine(config.TargetRootPath, config.ApplicationPath, "Common", "Interfaces", "Repositories", $"{interfaceName}.cs"),
            renderer.Render(TemplateNames.RepositoryInterface, new
            {
                repository_interface = interfaceName,
                entity_name = request.EntityName,
                entity_namespace = entityNamespace,
                @namespace = interfaceNamespace,
                interface_methods = interfaceMethods,
            }));

        plan.AddCreateFile(
            Path.Combine(config.RepositoriesPath, $"{implementationName}.cs"),
            renderer.Render(TemplateNames.RepositoryImplementation, new
            {
                repository_implementation = implementationName,
                repository_interface = interfaceName,
                entity_name = request.EntityName,
                entity_namespace = entityNamespace,
                interface_namespace = interfaceNamespace,
                @namespace = implementationNamespace,
                impl_methods = implMethods,
            }));

        if (request.AddDependencyInjectionRegistration)
        {
            AddDiRegistration(plan, interfaceName, implementationName);
        }

        return plan;
    }

    private static string GenerateInterfaceMethod(RepositoryMethodSpec m)
    {
        var allParams = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {StringUtilities.ToCamelCase(p.Name)}"));
        if (allParams.Length > 0) allParams += ", ";
        allParams += $"{GeneratorConstants.CancellationTokenType} {GeneratorConstants.CancellationTokenParamName} = default";
        return $"        {m.ReturnType} {m.Name}({allParams});";
    }

    private static string GenerateImplMethod(string entityName, RepositoryMethodSpec m)
    {
        var allParams = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {StringUtilities.ToCamelCase(p.Name)}"));
        if (allParams.Length > 0) allParams += ", ";
        allParams += $"{GeneratorConstants.CancellationTokenType} {GeneratorConstants.CancellationTokenParamName} = default";
        return $$"""
                public async {{m.ReturnType}} {{m.Name}}({{allParams}})
                {
                    {{GeneratorConstants.DefaultStubBody}}
                }
        """;
    }

    private void AddDiRegistration(GenerationPlan plan, string interfaceName, string implementationName)
    {
        try
        {
            plan.TransformFile(
                config.DependencyInjectionPath,
                content => editor.AddScopedRegistration(content, interfaceName, implementationName));
        }
        catch (InvalidOperationException exception)
        {
            plan.AddConflict(config.DependencyInjectionPath, exception.Message);
        }
    }

    private static void Validate(RepositoryGenerationRequest request)
    {
        CSharpNameValidator.EnsureIdentifier(request.EntityName, nameof(request.EntityName));
    }
}
