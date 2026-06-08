using System.Text.RegularExpressions;
using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class EntityGenerator(GeneratorConfig config, ScribanTemplateRenderer renderer)
{
    public GenerationPlan CreatePlan(EntityGenerationRequest request)
    {
        Validate(request);

        var plan = new GenerationPlan();

        var entityFolder = request.SubFolder is not null
            ? Path.Combine(config.TargetRootPath, "Domain", "Entities", request.SubFolder)
            : Path.Combine(config.TargetRootPath, "Domain", "Entities");

        var entityNamespace = request.Namespace
            ?? (request.SubFolder is not null
                ? $"{GeneratorConstants.DomainEntitiesNamespace}.{request.SubFolder}"
                : GeneratorConstants.DomainEntitiesNamespace);

        var entityPath = Path.Combine(entityFolder, $"{request.EntityName}.cs");

        plan.AddCreateFile(
            entityPath,
            renderer.Render(TemplateNames.Entity, new
            {
                entity_name = request.EntityName,
                @namespace = entityNamespace,
                has_properties = request.Properties.Count > 0,
                properties_block = string.Join("\n", request.Properties.Select(p => $"        public {p.Type} {p.Name} {{ get; set; }}")),
                has_factory = request.GenerateFactoryMethod,
                factory_params = string.Join(", ", request.Properties.Select(p => $"{p.Type} {StringUtilities.ToCamelCase(p.Name)}")),
                factory_assignments = string.Join("\n", request.Properties.Select(p => $"                {p.Name} = {StringUtilities.ToCamelCase(p.Name)},\n")),
                has_domain_methods = request.DomainMethods.Count > 0,
                domain_methods_block = string.Join("\n\n", request.DomainMethods.Select(m =>
                    $"        public void {m}()\n        {{\n            {GeneratorConstants.DefaultStubBody}\n        }}")),
            }));

        if (request.GenerateInterface)
        {
            var interfaceName = string.Format(GeneratorConstants.EntityInterfacePattern, request.EntityName);
            var interfacePath = Path.Combine(config.TargetRootPath, "Domain", "Interfaces", "Entities", $"{interfaceName}.cs");

            plan.AddCreateFile(
                interfacePath,
                renderer.Render(TemplateNames.EntityInterface, new
                {
                    entity_interface = interfaceName,
                    entity_name = request.EntityName,
                    @namespace = GeneratorConstants.DomainInterfacesEntitiesNamespace,
                    has_properties = request.Properties.Count > 0,
                    properties_block = string.Join("\n", request.Properties.Select(p => $"        {p.Type} {p.Name} {{ get; }}")),
                }));
        }

        if (request.GenerateEfMapping)
        {
            var mappingPath = Path.Combine(config.TargetRootPath, "Infrastructure", "Data", "EntitiesMapping", $"{request.EntityName}.cs");

            if (File.Exists(mappingPath))
            {
                var existing = File.ReadAllText(mappingPath);
                if (!existing.Contains("[Obsolete"))
                {
                    var modified = InsertObsoleteAttribute(existing, request.EntityName);
                    plan.AddUpdateFile(mappingPath, modified);
                }
                else
                {
                    plan.AddUpdateFile(mappingPath, existing);
                }

                plan.AddWarning($"EF mapping для '{request.EntityName}' уже существует. Добавлен [Obsolete]; требуется ручная проверка и исправление.");
            }
            else
            {
                var constructorAssignments = request.EfMappingFields is not null
                    ? string.Join("\n", request.EfMappingFields.Select(f =>
                        $"            this.{f.EfName} = entity.{f.DomainName};"))
                    : string.Join("\n", request.Properties.Select(p =>
                        CreateEfAssignment(p)));

                var toEntityAssignments = request.EfMappingFields is not null
                    ? string.Join("\n", request.EfMappingFields.Select(f =>
                        $"            entity.{f.DomainName} = this.{f.EfName};"))
                    : string.Join("\n", request.Properties.Select(p =>
                        CreateDomainAssignment(p)));

                plan.AddCreateFile(
                    mappingPath,
                    renderer.Render(TemplateNames.EfMapping, new
                    {
                        entity_name = request.EntityName,
                        domain_namespace = entityNamespace,
                        @namespace = GeneratorConstants.InfrastructureDataEntitiesNamespace,
                        has_properties = request.Properties.Count > 0,
                        constructor_assignments = constructorAssignments,
                        toentity_assignments = toEntityAssignments,
                    }));
            }
        }

        return plan;
    }

    private static string InsertObsoleteAttribute(string content, string entityName)
    {
        var obsolete = $"[Obsolete(\"TODO: требуется ручная проверка и исправление mapping-кода для {entityName}\", false)]";
        var pattern = $@"public\s+partial\s+class\s+{entityName}";
        return Regex.Replace(content, pattern, $"{obsolete}\n    $&");
    }

    private static string CreateEfAssignment(PropertySpec p)
    {
        var efName = ToEfPropertyName(p.Name);
        return $"            this.{efName} = entity.{p.Name};";
    }

    private static string CreateDomainAssignment(PropertySpec p)
    {
        var efName = ToEfPropertyName(p.Name);
        return $"            entity.{p.Name} = this.{efName};";
    }

    private static string ToEfPropertyName(string domainName)
    {
        if (domainName.EndsWith("Id", StringComparison.Ordinal) && domainName.Length > 2)
            return $"Id{domainName[..^2]}";
        return domainName;
    }

    private static void Validate(EntityGenerationRequest request)
    {
        CSharpNameValidator.EnsureIdentifier(request.EntityName, nameof(request.EntityName));
        CSharpNameValidator.NormalizeOptionalRelativePath(request.SubFolder, nameof(request.SubFolder));
        foreach (var p in request.Properties)
        {
            CSharpNameValidator.EnsureIdentifier(p.Name, nameof(p.Name));
            if (string.IsNullOrWhiteSpace(p.Type))
            {
                throw new ArgumentException("Property type is required.", nameof(p.Type));
            }
        }

        foreach (var methodName in request.DomainMethods)
        {
            CSharpNameValidator.EnsureIdentifier(methodName, nameof(request.DomainMethods));
        }
    }
}
