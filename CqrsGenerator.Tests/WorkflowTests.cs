using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Workflows;

namespace CqrsGenerator.Tests;

public class WorkflowTests
{
    [Fact]
    public void AddDtoWorkflow_CreatesDtoAndWebImports()
    {
        using var tmp = new TempProject();
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var factory = new CoreWorkflowFactory();

        var plan = factory.AddDto(config).CreatePlan(new AddDtoWorkflowRequest(
            "Users",
            "UserDto",
            [new("int", "Id")]));

        Assert.Contains(plan.Files, f => f.Path.EndsWith("UserDto.cs"));
        Assert.Contains(plan.Files, f => f.Path.EndsWith("_Imports.razor"));
        Assert.Contains("@using Application.Features.Users.DTOs", plan.Files.Single(f => f.Path.EndsWith("_Imports.razor")).Content);
    }

    [Fact]
    public void AddQueryWorkflow_WithCreateDto_CreatesDtoQueryHandlerAndSingleImportsFile()
    {
        using var tmp = new TempProject();
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var factory = new CoreWorkflowFactory();

        var plan = factory.AddQuery(config)
            .CreatePlan(new AddQueryWorkflowRequest(
                "Users",
                "GetUsers",
                new CreateLocalQueryDtoSelection("UserDto", [], "GetUsers"),
                ResponseShape.List,
                [new("int", "Id")],
                false,
                false,
                null));

        Assert.Contains(plan.Files, f => f.Path.EndsWith("UserDto.cs"));
        Assert.Contains(plan.Files, f => f.Path.EndsWith(Path.Combine("Queries", "GetUsers", "UserDto.cs")));
        Assert.Contains(plan.Files, f => f.Path.EndsWith("GetUsersQuery.cs"));
        Assert.Contains(plan.Files, f => f.Path.EndsWith("GetUsersHandler.cs"));

        var importsFiles = plan.Files.Where(f => f.Path.EndsWith("_Imports.razor")).ToArray();
        var imports = Assert.Single(importsFiles);
        Assert.Contains("@using Application.Features.Users.Queries.GetUsers", imports.Content);
        Assert.DoesNotContain("@using Application.Features.Users.DTOs", imports.Content);
    }

    [Fact]
    public void AddQueryWorkflow_WithCreateCustomDto_UsesCustomDtoProperties()
    {
        using var tmp = new TempProject();
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var factory = new CoreWorkflowFactory();

        var plan = factory.AddQuery(config)
            .CreatePlan(new AddQueryWorkflowRequest(
                "Users",
                "GetUsers",
                new CreateLocalQueryDtoSelection("UserLookupDto", [new("string", "DisplayName"), new("int", "Age")], "GetUsers"),
                ResponseShape.Single,
                [new("Guid", "Id")],
                false,
                false,
                null));

        var dtoFile = Assert.Single(plan.Files.Where(f => f.Path.EndsWith("UserLookupDto.cs")));
        Assert.Contains("public string DisplayName { get; set; }", dtoFile.Content);
        Assert.Contains("public int Age { get; set; }", dtoFile.Content);
        Assert.DoesNotContain("public Guid Id { get; set; }", dtoFile.Content);
    }

    [Fact]
    public void AddQueryWorkflow_WithNewQueryService_CreatesImplementationInFeatureAwarePath()
    {
        using var tmp = new TempProject();
        tmp.AddFile("Infrastructure/DependencyInjection.cs", """
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }
}
""");
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var factory = new CoreWorkflowFactory();

        var plan = factory.AddQuery(config)
            .CreatePlan(new AddQueryWorkflowRequest(
                "Users",
                "GetUsers",
                new UseSharedFeatureDtoSelection("UserDto", "Application.Features.Users.DTOs", string.Empty),
                ResponseShape.Single,
                [],
                false,
                false,
                new QueryServiceMethodWorkflowRequest(
                    "IUsersQueryService",
                    "UsersQueryService",
                    null,
                    null,
                    true,
                    true,
                    true,
                    false,
                    "GetUsersAsync",
                    "Task<UserDto>",
                    "UserDto")));

        Assert.Contains(plan.Files, file => file.Path.EndsWith(Path.Combine("Infrastructure", "Data", "QueryServices", "UsersQueryService.cs")));
    }

    [Fact]
    public void AddQueryWorkflow_WithNestedFeature_CreatesQueryServiceImplementationInParentFeatureSubfolder()
    {
        using var tmp = new TempProject();
        tmp.AddFile("Infrastructure/DependencyInjection.cs", """
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }
}
""");
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var factory = new CoreWorkflowFactory();

        var plan = factory.AddQuery(config)
            .CreatePlan(new AddQueryWorkflowRequest(
                "Document/GroupsAccess",
                "GetGroups",
                new UseSharedFeatureDtoSelection("GroupDto", "Application.Features.Document.GroupsAccess.DTOs", string.Empty),
                ResponseShape.Single,
                [],
                false,
                false,
                new QueryServiceMethodWorkflowRequest(
                    "IGroupsAccessQueryService",
                    "GroupsAccessQueryService",
                    null,
                    null,
                    true,
                    true,
                    true,
                    false,
                    "GetGroupsAsync",
                    "Task<GroupDto>",
                    "GroupDto")));

        Assert.Contains(plan.Files, file => file.Path.EndsWith(Path.Combine("Infrastructure", "Data", "QueryServices", "Document", "GroupsAccessQueryService.cs")));
    }

    [Fact]
    public void AddQueryWorkflow_PromotesLocalDtoIntoSharedDtosAndDeletesSourceFile()
    {
        using var tmp = new TempProject();
        var sourceDtoPath = tmp.AddFile(
            "Application/Features/Users/Queries/GetUsers/UserDto.cs",
            """
namespace Application.Features.Users.Queries.GetUsers{
public class UserDto
{
    public int Id { get; set; }
}
}
""");
        var ownerQueryPath = tmp.AddFile(
            "Application/Features/Users/Queries/GetUsers/GetUsersQuery.cs",
            """
namespace Application.Features.Users.Queries.GetUsers;

public class GetUsersQuery
{
    public UserDto Result { get; set; } = new();
}
""");
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var factory = new CoreWorkflowFactory();

        var plan = factory.AddQuery(config).CreatePlan(new AddQueryWorkflowRequest(
            "Users",
            "FindUsers",
            new PromoteLocalQueryDtoSelection(
                "UserDto",
                sourceDtoPath,
                "Application.Features.Users.Queries.GetUsers",
                "GetUsers",
                "Users"),
            ResponseShape.Single,
            [],
            false,
            false,
            null));

        Assert.Contains(plan.Operations, operation => operation is CreateFileOperation create
            && create.Path.EndsWith(Path.Combine("Application", "Features", "Users", "DTOs", "UserDto.cs"))
            && create.Content.Contains(
                """
namespace Application.Features.Users.DTOs
{
""",
                StringComparison.Ordinal));
        Assert.Contains(plan.Operations, operation => operation is DeleteFileOperation delete
            && delete.Path == sourceDtoPath);
        Assert.Contains(plan.Operations, operation => operation is UpdateFileOperation update
            && update.Path == ownerQueryPath
            && update.Content.Contains("using Application.Features.Users.DTOs;", StringComparison.Ordinal));
    }

    [Fact]
    public void RazorImportsGenerator_ExistingFile_AddsOnlyMissingUsing()
    {
        using var tmp = new TempProject();
        tmp.AddFile("Web/Features/Users/_Imports.razor", "@using Application.Features.Users.DTOs");
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);

        var plan = new RazorImportsGenerator(config).AddUsings(
            "Users",
            [
                "Application.Features.Users.DTOs",
                "Application.Features.Users.Queries.GetUsers",
            ]);

        var update = Assert.IsType<UpdateFileOperation>(Assert.Single(plan.Operations));
        Assert.Contains("@using Application.Features.Users.DTOs", update.Content);
        Assert.Contains("@using Application.Features.Users.Queries.GetUsers", update.Content);
        Assert.Single(update.Content.Split(["\r\n", "\n"], StringSplitOptions.None), line => line == "@using Application.Features.Users.DTOs");
    }

    [Fact]
    public void CreateFeatureBundleWorkflow_CreatesFeatureStructureAndQueryService()
    {
        using var tmp = new TempProject();
        tmp.AddFile("Infrastructure/DependencyInjection.cs", """
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }
}
""");
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);

        var factory = new CoreWorkflowFactory();
        var plan = factory.CreateFeatureBundle(config)
            .CreatePlan(new CreateFeatureBundleWorkflowRequest(
                "Users",
                new QueryServiceGenerationRequest
                {
                    FeaturePath = "Users",
                    InterfaceName = "IUsersQueryService",
                    ImplementationName = "UsersQueryService",
                    ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(config, "Users", "UsersQueryService"),
                    ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(config, "Users"),
                    AddDependencyInjectionRegistration = true,
                }));

        Assert.Empty(plan.Conflicts);
        Assert.Contains(plan.Operations, operation => operation.Path.EndsWith(Path.Combine("Features", "Users")));
        Assert.Contains(plan.Files, file => file.Path.EndsWith("IUsersQueryService.cs"));
        Assert.Contains(plan.Files, file => file.Path.EndsWith("UsersQueryService.cs"));

        var update = Assert.IsType<UpdateFileOperation>(
            Assert.Single(plan.Operations.OfType<UpdateFileOperation>()));
        Assert.Contains("services.AddScoped<IUsersQueryService, UsersQueryService>();", update.Content);
    }
}
