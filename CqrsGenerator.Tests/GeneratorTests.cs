using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Templates;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Workflows;

namespace CqrsGenerator.Tests;

public class GeneratorTests
{
    private static GeneratorConfig Config => GeneratorConfig.ForTargetRoot("C:\\fake-root");
    private static ScribanTemplateRenderer Renderer => new(new TemplateProvider());

    // ═══ QueryGenerator (13) ═══

    [Fact]
    public void QueryGenerator_WithoutService_NoInjection()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
        });

        var handler = plan.Files.First(f => f.Path.EndsWith("Handler.cs"));
        Assert.DoesNotContain("private readonly", handler.Content);
        Assert.Contains("throw new NotImplementedException()", handler.Content);
    }

    [Fact]
    public void QueryGenerator_WithService_ButNoBody_HasInjectionWithStub()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
            ServiceInterfaceName = "ITestService", ServiceMethodName = "GetDataAsync", GenerateHandlerBody = false,
        });

        var handler = plan.Files.First(f => f.Path.EndsWith("Handler.cs"));
        Assert.Contains("private readonly ITestService _testService", handler.Content);
        Assert.Contains("throw new NotImplementedException()", handler.Content);
    }

    [Fact]
    public void QueryGenerator_WithServiceAndBody_GeneratesCall()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
            ServiceInterfaceName = "ITestService", ServiceMethodName = "GetDataAsync", GenerateHandlerBody = true,
            Properties = [new("int", "Id")],
        });

        var handler = plan.Files.First(f => f.Path.EndsWith("Handler.cs"));
        Assert.Contains("return await _testService.GetDataAsync(request.Id, cancellationToken);", handler.Content);
    }

    [Fact]
    public void QueryGenerator_CreateDto_True_DoesNotCreateDtoBecauseGeneratorIsAtomic()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", CreateDto = true, ResponseShape = ResponseShape.Single,
        });

        Assert.All(plan.Files, f => Assert.DoesNotContain("DTOs", f.Path));
    }

    [Fact]
    public void QueryGenerator_CreateDto_False_NoDtoFile()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", CreateDto = false, ResponseShape = ResponseShape.Single,
        });

        Assert.All(plan.Files, f => Assert.DoesNotContain("DTOs", f.Path));
    }

    [Fact]
    public void QueryGenerator_ResponseShape_Single()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
        });
        var query = plan.Files.First(f => f.Path.EndsWith("Query.cs"));
        Assert.Contains("IQuery<UserDto>", query.Content);
    }

    [Fact]
    public void QueryGenerator_ResponseShape_Enumerable()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Enumerable,
        });
        var query = plan.Files.First(f => f.Path.EndsWith("Query.cs"));
        Assert.Contains("IQuery<IEnumerable<UserDto>>", query.Content);
    }

    [Fact]
    public void QueryGenerator_ResponseShape_List()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.List,
        });
        var query = plan.Files.First(f => f.Path.EndsWith("Query.cs"));
        Assert.Contains("IQuery<List<UserDto>>", query.Content);
    }

    [Fact]
    public void QueryGenerator_EmptyProperties_NoConstructor()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
        });
        var query = plan.Files.First(f => f.Path.EndsWith("Query.cs"));
        Assert.DoesNotContain("public GetUsersQuery(", query.Content);
    }

    [Fact]
    public void QueryGenerator_WithProperties_GeneratesConstructor()
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
            Properties = [new("int", "AbonentId"), new("string", "Name")],
        });
        var query = plan.Files.First(f => f.Path.EndsWith("Query.cs"));
        Assert.Contains("public GetUsersQuery(int abonentId, string name)", query.Content);
        Assert.Contains("public int AbonentId { get; }", query.Content);
        Assert.Contains("public string Name { get; }", query.Content);
    }

    [Fact]
    public void QueryGenerator_Validate_BadFeaturePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
        }));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("bool")]
    [InlineData("DateTime")]
    public void QueryGenerator_CreateDtoFalse_PrimitiveType_DoesNotThrow(string typeName)
    {
        var plan = new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetX", DtoName = typeName,
            ResponseShape = ResponseShape.Single, CreateDto = false,
        });
        Assert.Contains(plan.Files, f => f.Path.EndsWith("Handler.cs"));
    }

    [Fact]
    public void QueryGenerator_Validate_EmptyDtoName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "", ResponseShape = ResponseShape.Single,
            CreateDto = true,
        }));
    }

    [Fact]
    public void QueryGenerator_Validate_EmptyPropertyType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new QueryGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", QueryName = "GetUsers", DtoName = "UserDto", ResponseShape = ResponseShape.Single,
            Properties = [new("", "Name")],
        }));
    }

    // ═══ CommandGenerator (8) ═══

    [Fact]
    public void CommandGenerator_WithoutResponse_GeneratesICommand()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", CommandName = "CreateUser",
        });
        var cmd = plan.Files.First(f => f.Path.EndsWith("Command.cs"));
        var handler = plan.Files.First(f => f.Path.EndsWith("Handler.cs"));

        Assert.Contains("ICommand", cmd.Content);
        Assert.DoesNotContain("ICommand<", cmd.Content);
        Assert.Contains("Task Handle", handler.Content);
        Assert.DoesNotContain("Task<", handler.Content);
    }

    [Fact]
    public void CommandGenerator_WithResponse_ReturnsTaskT()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", CommandName = "CreateUser", ResponseType = "int",
        });
        var cmd = plan.Files.First(f => f.Path.EndsWith("Command.cs"));
        var handler = plan.Files.First(f => f.Path.EndsWith("Handler.cs"));

        Assert.Contains("ICommand<int>", cmd.Content);
        Assert.Contains("Task<int> Handle", handler.Content);
        Assert.Contains("UnitOfWorkBehavior", plan.Warnings.Single().Message);
    }

    [Fact]
    public void CommandGenerator_WithDependencies_AddsConstructor()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", CommandName = "CreateUser",
            Dependencies = [new("IHRepo", "HRepo"), new("ICurrentUserContext", "CurrentUserContext")],
        });
        var handler = plan.Files.First(f => f.Path.EndsWith("Handler.cs"));

        Assert.Contains("private readonly IHRepo _hRepo", handler.Content);
        Assert.Contains("private readonly ICurrentUserContext _currentUserContext", handler.Content);
        Assert.Contains("public CreateUserHandler(IHRepo hRepo, ICurrentUserContext currentUserContext)", handler.Content);
    }

    [Fact]
    public void CommandGenerator_NoDependencies_NoConstructor()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", CommandName = "CreateUser",
        });
        var handler = plan.Files.First(f => f.Path.EndsWith("Handler.cs"));
        Assert.DoesNotContain("private readonly", handler.Content);
    }

    [Fact]
    public void CommandGenerator_WithProperties_GeneratesConstructor()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", CommandName = "CreateUser",
            Properties = [new("int", "Id"), new("string", "Name")],
        });
        var cmd = plan.Files.First(f => f.Path.EndsWith("Command.cs"));
        Assert.Contains("public CreateUserCommand(int id, string name)", cmd.Content);
        Assert.Contains("public int Id { get; }", cmd.Content);
    }

    [Fact]
    public void CommandGenerator_Validate_BadCommandName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", CommandName = "class",
        }));
    }

    [Fact]
    public void CommandGenerator_Validate_EmptyPropertyType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", CommandName = "CreateUser",
            Properties = [new("", "Name")],
        }));
    }

    // ═══ DtoGenerator (5) ═══

    [Fact]
    public void DtoGenerator_NoProperties_EmptyClass()
    {
        var plan = new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", DtoName = "EmptyDto",
        });
        var file = Assert.Single(plan.Files);
        Assert.Contains("public class EmptyDto", file.Content);
        Assert.DoesNotContain("{ get; set; }", file.Content);
    }

    [Fact]
    public void DtoGenerator_WithProperties_GeneratesSetters()
    {
        var plan = new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", DtoName = "UserDto",
            Properties = [new("int", "Id"), new("string", "Name")],
        });
        var file = Assert.Single(plan.Files);
        Assert.Contains("public int Id { get; set; }", file.Content);
        Assert.Contains("public string Name { get; set; }", file.Content);
    }

    [Fact]
    public void DtoGenerator_NullSubfolder_NoSubfolderInPath()
    {
        var plan = new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", DtoName = "Dto", Subfolder = null,
        });
        var file = Assert.Single(plan.Files);
        AssertPathEndsWith(file.Path, "DTOs", "Dto.cs");
    }

    [Fact]
    public void DtoGenerator_EmptySubfolder_NoSubfolderInPath()
    {
        var plan = new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", DtoName = "Dto", Subfolder = "",
        });
        var file = Assert.Single(plan.Files);
        AssertPathEndsWith(file.Path, "DTOs", "Dto.cs");
    }

    [Fact]
    public void DtoGenerator_WithNestedSubfolder_NormalizesPath()
    {
        var plan = new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", DtoName = "Dto", Subfolder = "Admin\\Internal",
        });

        var file = Assert.Single(plan.Files);
        AssertPathEndsWith(file.Path, "DTOs", "Admin", "Internal", "Dto.cs");
        Assert.Contains("namespace Application.Features.Test.DTOs.Admin.Internal", file.Content);
    }

    [Fact]
    public void DtoGenerator_UnsafeSubfolder_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", DtoName = "Dto", Subfolder = "../Queries",
        }));
    }

    [Fact]
    public void DtoGenerator_Validate_BadDtoName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test", DtoName = "",
        }));
    }

    // ═══ QueryServiceGenerator (13) ═══

    [Fact]
    public void QueryServiceCreator_NoMethod_GeneratesInterfaceAndImpl()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
        });

        Assert.Equal(2, plan.Files.Count);
        Assert.Contains(plan.Files, f => f.Path.EndsWith("ITestQueryService.cs"));
        Assert.Contains(plan.Files, f => f.Path.EndsWith("TestQueryService.cs"));
    }

    [Fact]
    public void QueryServiceCreator_WithMethodStub_GeneratesBody()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<string>", InitialMethodName = "GetDataAsync",
        });

        Assert.Equal(2, plan.Files.Count);
        Assert.Contains(plan.Files, f => f.Path.EndsWith("ITestQueryService.cs"));
        Assert.Contains(plan.Files, f => f.Path.EndsWith("TestQueryService.cs"));
    }

    [Fact]
    public void QueryServiceCreator_CreatesImplementationInCanonicalRoot()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
        });

        var implementation = Assert.Single(plan.Files, f => Path.GetFileName(f.Path) == "TestQueryService.cs");
        Assert.Contains(Path.Combine("Infrastructure", "Data", "QueryServices", "TestQueryService.cs"), implementation.Path);
    }

    [Fact]
    public void QueryServiceCreator_ForNestedFeature_CreatesImplementationInParentFeatureSubfolder()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Document/GroupsAccess", InterfaceName = "IGroupsAccessQueryService", ImplementationName = "GroupsAccessQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Document/GroupsAccess", "GroupsAccessQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Document/GroupsAccess"),
        });

        var implementation = Assert.Single(plan.Files, f => Path.GetFileName(f.Path) == "GroupsAccessQueryService.cs");
        Assert.Contains(Path.Combine("Infrastructure", "Data", "QueryServices", "Document", "GroupsAccessQueryService.cs"), implementation.Path);
        Assert.Contains("namespace Infrastructure.Data.QueryServices.Document", implementation.Content);
    }

    [Fact]
    public void QueryServiceCreator_WithDapperBody_UsesExecuteAsync()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<IEnumerable<UserDto>>", InitialMethodName = "GetUsersAsync",
            GenerateImplementationBody = true, DtoTypeName = "UserDto",
            InitialParameters = [new("int", "TabNumber")],
        });

        Assert.Equal(2, plan.Files.Count);
        Assert.Contains(plan.Files, f => f.Path.EndsWith("ITestQueryService.cs"));
        Assert.Contains(plan.Files, f => f.Path.EndsWith("TestQueryService.cs"));
    }

    [Fact]
    public void QueryServiceCreator_EmptyParams_DapperBodyWithoutNew()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<IEnumerable<UserDto>>", InitialMethodName = "GetAllAsync",
            GenerateImplementationBody = true, DtoTypeName = "UserDto",
        });

        Assert.Equal(2, plan.Files.Count);
        Assert.Contains(plan.Files, f => f.Path.EndsWith("TestQueryService.cs"));
    }

    [Fact]
    public void QueryServiceCreator_DiRegistration_True_UpdatesDi()
    {
        using var tmp = new TempProject();
        tmp.AddFile("Infrastructure/DependencyInjection.cs", """
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }

}
""");
        var tmpConfig = GeneratorConfig.ForTargetRoot(tmp.Root);
        var gen = new QueryServiceGenerator(tmpConfig, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(tmpConfig, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(tmpConfig, "Test"),
            AddDependencyInjectionRegistration = true,
        });

        Assert.Contains(plan.Operations, o => o.Kind == GenerationOperationKind.UpdateFile
            && o.Path.EndsWith("DependencyInjection.cs"));
    }

    [Fact]
    public void QueryServiceCreator_DtoTypeNameProvided_UsesIt()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<Foo>", InitialMethodName = "GetFooAsync",
            GenerateImplementationBody = true, DtoTypeName = "MyCustomDto",
        });

        Assert.Equal(2, plan.Files.Count);
        Assert.Contains(plan.Files, f => f.Path.EndsWith("TestQueryService.cs"));
    }

    [Fact]
    public void QueryServiceCreator_ExtractDtoType_FromTaskT()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<FooDto>", InitialMethodName = "GetFooAsync",
            GenerateImplementationBody = true,
        });

        Assert.Equal(2, plan.Files.Count);
        Assert.Contains(plan.Files, f => f.Path.EndsWith("ITestQueryService.cs"));
        Assert.Contains(plan.Files, f => f.Path.EndsWith("TestQueryService.cs"));
    }

    [Fact]
    public void QueryServiceCreator_MethodParams_IncludeCancellationToken()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<int>", InitialMethodName = "GetAsync",
            InitialParameters = [new("string", "Name")],
        });

        var intf = plan.Files.First(f => f.Path.EndsWith("ITestQueryService.cs"));
        Assert.Contains("CancellationToken ct = default", intf.Content);
        var impl = plan.Files.First(f => f.Path.EndsWith("TestQueryService.cs"));
        Assert.Contains("CancellationToken ct = default", impl.Content);
    }

    [Fact]
    public void AddMethodPlan_InterfaceFileMissing_AddsConflict()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.AddMethodPlan(new()
        {
            InterfacePath = "C:\\missing\\IFoo.cs",
            ImplementationPath = "C:\\missing\\Foo.cs",
            InterfaceName = "IFoo", ImplementationName = "Foo",
            ReturnType = "Task<int>", MethodName = "GetAsync",
        });

        Assert.NotEmpty(plan.Conflicts);
        Assert.Contains(plan.Conflicts, c => c.Path.Contains("IFoo.cs"));
    }

    [Fact]
    public void AddMethodPlan_BothFilesExist_AddsMethod()
    {
        using var tmp = new TempProject();
        var ifacePath = tmp.AddFile("IFoo.cs", """
namespace Ns
{
    public interface IFoo
    {
    }
}
""");
        var implPath = tmp.AddFile("Foo.cs", """
namespace Ns
{
    public class Foo
    {
    }
}
""");
        var gen = new QueryServiceGenerator(Config, Renderer, new());

        var plan = gen.AddMethodPlan(new()
        {
            InterfacePath = ifacePath, ImplementationPath = implPath,
            InterfaceName = "IFoo", ImplementationName = "Foo",
            ReturnType = "Task<int>", MethodName = "GetAsync",
            Parameters = [new("string", "Name")],
        });

        Assert.Empty(plan.Conflicts);
        Assert.Equal(2, plan.Operations.Count);

        var interfaceUpdate = Assert.IsType<UpdateFileOperation>(
            plan.Operations.Single(operation => operation.Path == Path.GetFullPath(ifacePath)));
        var implementationUpdate = Assert.IsType<UpdateFileOperation>(
            plan.Operations.Single(operation => operation.Path == Path.GetFullPath(implPath)));

        Assert.Contains("""
        Task<int> GetAsync(string name, CancellationToken ct = default);
    }
}
""", interfaceUpdate.Content);
        Assert.DoesNotContain("\n            Task<int> GetAsync", interfaceUpdate.Content);

        Assert.Contains("""
        public async Task<int> GetAsync(string name, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
""", implementationUpdate.Content);
        Assert.DoesNotContain("\n            public async Task<int> GetAsync", implementationUpdate.Content);
    }

    [Fact]
    public void AddMethodPlan_GenerateBody_UsesDapper()
    {
        using var tmp = new TempProject();
        var ifacePath = tmp.AddFile("IFoo.cs", "namespace Ns; public interface IFoo { }");
        var implPath = tmp.AddFile("Foo.cs", "namespace Ns; public class Foo { }");
        var gen = new QueryServiceGenerator(Config, Renderer, new());

        var plan = gen.AddMethodPlan(new()
        {
            InterfacePath = ifacePath, ImplementationPath = implPath,
            InterfaceName = "IFoo", ImplementationName = "Foo",
            ReturnType = "Task<IEnumerable<SomeDto>>", MethodName = "GetAllAsync",
            GenerateImplementationBody = true, DtoTypeName = "SomeDto",
        });

        Assert.Empty(plan.Conflicts);
        Assert.Equal(2, plan.Operations.Count);
    }

    [Fact]
    public void AddMethodPlan_MissingImplementation_AddsConflict()
    {
        using var tmp = new TempProject();
        var ifacePath = tmp.AddFile("IFoo.cs", "namespace Ns; public interface IFoo { }");
        var gen = new QueryServiceGenerator(Config, Renderer, new());

        var plan = gen.AddMethodPlan(new()
        {
            InterfacePath = ifacePath, ImplementationPath = "C:\\missing\\Foo.cs",
            InterfaceName = "IFoo", ImplementationName = "Foo",
            ReturnType = "Task<int>", MethodName = "GetAsync",
        });

        Assert.NotEmpty(plan.Conflicts);
        Assert.Contains(plan.Conflicts, c => c.Path.Contains("Foo.cs"));
    }

    // ═══ WebPageGenerator (9) ═══

    [Fact]
    public void WebPageGenerator_WithImports_CreatesImportsFile()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test", PageName = "TestPage", Route = "/test", CreateImports = true,
        });
        Assert.Contains(plan.Files, f => f.Path.EndsWith("_Imports.razor"));
    }

    [Fact]
    public void WebPageGenerator_ExistingImports_AddsOnlyMissingUsings()
    {
        using var tmp = new TempProject();
        tmp.AddFile("Web/Features/Users/_Imports.razor", "@using Application.Features.Users");
        var config = GeneratorConfig.ForTargetRoot(tmp.Root);
        var gen = new WebPageGenerator(config, Renderer);

        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Users", PageName = "UsersPage", Route = "/users", CreateImports = true,
        });

        var update = Assert.IsType<UpdateFileOperation>(Assert.Single(plan.Operations.OfType<UpdateFileOperation>()));
        Assert.Equal(Path.Combine(tmp.Root, "Web", "Features", "Users", "_Imports.razor"), update.Path);
        Assert.Contains("@using Application.Features.Users", update.Content);
        Assert.Contains("@using Web.Features.Users", update.Content);
        Assert.Single(update.Content.Split(["\r\n", "\n"], StringSplitOptions.None), line => line == "@using Application.Features.Users");
    }

    [Fact]
    public void WebPageGenerator_PlannedImports_ComposesWithoutDuplicateFileOperation()
    {
        var basePlan = new GenerationPlan();
        var importsPath = Path.Combine(Config.WebFeatureRootPath, "Users", "_Imports.razor");
        basePlan.AddCreateFile(importsPath, "@using Existing.Namespace");

        new WebPageGenerator(Config, Renderer).ApplyToPlan(basePlan, new()
        {
            WebFeaturePath = "Users", PageName = "UsersPage", Route = "/users", CreateImports = true,
        });

        var imports = Assert.Single(basePlan.Operations, operation => operation.Path == importsPath);
        var create = Assert.IsType<CreateFileOperation>(imports);
        Assert.Contains("@using Existing.Namespace", create.Content);
        Assert.Contains("@using Application.Features.Users", create.Content);
        Assert.Contains("@using Web.Features.Users", create.Content);
        Assert.DoesNotContain(basePlan.Conflicts, conflict => conflict.Path == importsPath);
    }

    [Fact]
    public void WebPageGenerator_RouteWithAbonentId_GeneratesParameter()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test", PageName = "TestPage", Route = "/test/{abonentId:int}",
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.Contains("[Parameter]", page.Content);
        Assert.Contains("public int AbonentId", page.Content);
    }

    [Fact]
    public void WebPageGenerator_RouteWithMultipleParameters_GeneratesAll()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test", PageName = "TestPage", Route = "/{groupId:int}/{name}",
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.Contains("public int GroupId", page.Content);
        Assert.Contains("public string Name", page.Content);
    }

    [Fact]
    public void WebPageGenerator_RouteWithoutSlash_Throws()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        Assert.Throws<ArgumentException>(() => gen.CreatePlan(new()
        {
            WebFeaturePath = "Test", PageName = "TestPage", Route = "no-slash",
        }));
    }

    [Fact]
    public void WebPageGenerator_WithOneQuery_GeneratesDataGrid()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test",
            PageName = "ItemsPage",
            Route = "/items",
            Queries =
            [
                new WebPageQueryBinding(
                    QueryName: "GetItemsQuery",
                    VariableName: "_items",
                    Args: "",
                    ResultTypeName: "ItemDto",
                    ResponseShape: ResponseShape.List,
                    HasRefresh: true),
            ],
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.Contains("TItem=\"ItemDto\"", page.Content);
        Assert.Contains("AllowPaging=\"true\"", page.Content);
    }

    [Fact]
    public void WebPageGenerator_WithOneQuery_GeneratesOnInitializedWithMediatorCall()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test",
            PageName = "ItemsPage",
            Route = "/items",
            Queries =
            [
                new WebPageQueryBinding(
                    QueryName: "GetItemsQuery",
                    VariableName: "_items",
                    Args: "AbonentId",
                    ResultTypeName: "ItemDto",
                    ResponseShape: ResponseShape.List,
                    HasRefresh: true),
            ],
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.Contains("await Mediator.SendAsync(new GetItemsQuery(AbonentId))", page.Content);
    }

    [Fact]
    public void WebPageGenerator_WithOneQuery_GeneratesRefreshMethod()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test",
            PageName = "ItemsPage",
            Route = "/items",
            Queries =
            [
                new WebPageQueryBinding(
                    QueryName: "GetItemsQuery",
                    VariableName: "_items",
                    Args: "",
                    ResultTypeName: "ItemDto",
                    ResponseShape: ResponseShape.List,
                    HasRefresh: true),
            ],
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        var content = page.Content;
        Assert.Contains("private async Task LoadItemsAsync() =>", content);
        Assert.Contains("_items = await Mediator.SendAsync(new GetItemsQuery())", content);
    }

    [Fact]
    public void WebPageGenerator_WithMultipleQueries_GeneratesAllVariablesAndInvocations()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test",
            PageName = "DashboardPage",
            Route = "/dashboard",
            Queries =
            [
                new WebPageQueryBinding("GetItemsQuery", "_items", "", "ItemDto", ResponseShape.List, HasRefresh: true),
                new WebPageQueryBinding("GetCategoriesQuery", "_categories", "", "CategoryDto", ResponseShape.List, HasRefresh: false),
                new WebPageQueryBinding("GetStatusesQuery", "_statuses", "", "StatusDto", ResponseShape.List, HasRefresh: false),
            ],
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.Contains("IEnumerable<ItemDto> _items", page.Content);
        Assert.Contains("IEnumerable<CategoryDto> _categories", page.Content);
        Assert.Contains("IEnumerable<StatusDto> _statuses", page.Content);
        Assert.Contains("await Mediator.SendAsync(new GetItemsQuery())", page.Content);
        Assert.Contains("await Mediator.SendAsync(new GetCategoriesQuery())", page.Content);
        Assert.Contains("await Mediator.SendAsync(new GetStatusesQuery())", page.Content);
    }

    [Fact]
    public void WebPageGenerator_NoQueries_GeneratesPlaceholder()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test", PageName = "SimplePage", Route = "/simple",
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.Contains("// await Mediator.SendAsync(new YourQuery())", page.Content);
        Assert.DoesNotContain("_isFirstLoad", page.Content);
        Assert.DoesNotContain("IEnumerable<", page.Content);
    }

    [Fact]
    public void WebPageGenerator_SingleResponse_GeneratesNonNullableField()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test",
            PageName = "DetailPage",
            Route = "/detail",
            Queries =
            [
                new WebPageQueryBinding("GetItemQuery", "_item", "", "ItemDto", ResponseShape.Single, HasRefresh: false),
            ],
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.Contains("ItemDto _item = default!", page.Content);
        Assert.DoesNotContain("IEnumerable", page.Content);
    }

    [Fact]
    public void WebPageGenerator_HasRefreshFalse_SkipsRefreshMethod()
    {
        var gen = new WebPageGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            WebFeaturePath = "Test",
            PageName = "ItemsPage",
            Route = "/items",
            Queries =
            [
                new WebPageQueryBinding("GetItemsQuery", "_items", "", "ItemDto", ResponseShape.List, HasRefresh: false),
            ],
        });
        var page = plan.Files.First(f => f.Path.EndsWith(".razor"));
        Assert.DoesNotContain("LoadItemsAsync", page.Content);
    }

    // ═══ EntityGenerator (6) ═══

    [Fact]
    public void EntityGenerator_NoSubFolder_PlacesInRoot()
    {
        var gen = new EntityGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            EntityName = "MyEntity",
            Properties = [new("int", "AbonentId")],
        });
        var entity = plan.Files.First(f => f.Path.EndsWith("MyEntity.cs") && f.Path.Contains("Domain"));
        AssertPathEndsWith(entity.Path, "Domain", "Entities", "MyEntity.cs");
        Assert.Contains("namespace Domain.Entities", entity.Content);
    }

    [Fact]
    public void EntityGenerator_WithSubFolder_UsesSubFolder()
    {
        var gen = new EntityGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            EntityName = "HApplication",
            SubFolder = "Application",
            Properties = [new("int", "AbonentId")],
        });
        var entity = plan.Files.First(f => f.Path.EndsWith("HApplication.cs") && f.Path.Contains("Domain"));
        AssertPathEndsWith(entity.Path, "Domain", "Entities", "Application", "HApplication.cs");
        Assert.Contains("namespace Domain.Entities.Application", entity.Content);
    }

    [Fact]
    public void EntityGenerator_NoEfMapping_DoesNotCreateMappingFile()
    {
        var gen = new EntityGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            EntityName = "MyEntity",
            GenerateEfMapping = false,
        });
        Assert.DoesNotContain(plan.Files, f => f.Path.Contains("EntitiesMapping"));
    }

    [Fact]
    public void EntityGenerator_WithEfMappingCreatesMappingFile()
    {
        var gen = new EntityGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            EntityName = "MyEntity",
            Properties = [new("int", "AbonentId"), new("string", "Name")],
            GenerateEfMapping = true,
            EfMappingFields = [("AbonentId", "IdAbonent"), ("Name", "Name")],
        });
        var mapping = plan.Files.First(f => f.Path.EndsWith("MyEntity.cs") && f.Path.Contains("EntitiesMapping"));
        Assert.Contains("public MyEntity()", mapping.Content);
        Assert.Contains("public MyEntity(Entity entity)", mapping.Content);
        Assert.Contains("this.IdAbonent = entity.AbonentId", mapping.Content);
        Assert.Contains("entity.Name = this.Name", mapping.Content);
        Assert.Contains("((IBaseEntity)entity).SetId(this.Id)", mapping.Content);
    }

    [Fact]
    public void EntityGenerator_ExistingMapping_AddsObsoleteAndWarning()
    {
        using var tmp = new TempProject();
        var mappingDir = tmp.AddDir("Infrastructure/Data/EntitiesMapping");
        var mappingPath = Path.Combine(mappingDir, "MyEntity.cs");
        File.WriteAllText(mappingPath, """
using Infrastructure.Data.IdTracking;

namespace Infrastructure.Data.Entities
{
    public partial class MyEntity : IEfEntity
    {
        public MyEntity(Entity entity) : this() { }
        public Entity ToEntity() => new();
    }
}
""");
        var tmpConfig = GeneratorConfig.ForTargetRoot(tmp.Root);
        var gen = new EntityGenerator(tmpConfig, Renderer);
        var plan = gen.CreatePlan(new()
        {
            EntityName = "MyEntity",
            GenerateEfMapping = true,
        });

        var updateOp = plan.Operations.OfType<UpdateFileOperation>()
            .FirstOrDefault(o => o.Path.Contains("EntitiesMapping"));
        Assert.NotNull(updateOp);
        Assert.Contains("[Obsolete", updateOp.Content);
        Assert.NotEmpty(plan.Warnings);
        Assert.Contains("уже существует", plan.Warnings[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EntityGenerator_FactoryMethod_GeneratesCreateFactory()
    {
        var gen = new EntityGenerator(Config, Renderer);
        var plan = gen.CreatePlan(new()
        {
            EntityName = "Permission",
            Properties = [new("int", "AbonentId"), new("int", "DopParamId")],
            GenerateFactoryMethod = true,
        });
        var entity = plan.Files.First(f => f.Path.EndsWith("Permission.cs") && f.Path.Contains("Domain"));
        Assert.Contains("public static Permission Create(", entity.Content);
        Assert.Contains("AbonentId = abonentId", entity.Content);
        Assert.Contains("DopParamId = dopParamId", entity.Content);
    }

    // ═══ RepositoryGenerator (4) ═══

    [Fact]
    public void RepositoryGenerator_DefaultNamespace_GeneratesInterface()
    {
        var gen = new RepositoryGenerator(Config, Renderer, new());
        var plan = gen.CreatePlan(new()
        {
            EntityName = "HApplication",
        });
        var intf = plan.Files.First(f => f.Path.EndsWith("IHApplicationRepository.cs"));
        Assert.Contains("public interface IHApplicationRepository", intf.Content);
        Assert.Contains("Application.Common.Interfaces.Repositories", intf.Content);
    }

    [Fact]
    public void RepositoryGenerator_CustomNamespace_UsesIt()
    {
        var gen = new RepositoryGenerator(Config, Renderer, new());
        var plan = gen.CreatePlan(new()
        {
            EntityName = "Foo", Namespace = "My.Custom.Namespace",
        });
        var intf = plan.Files.First(f => f.Path.EndsWith("IFooRepository.cs"));
        Assert.Contains("My.Custom.Namespace", intf.Content);
    }

    [Fact]
    public void RepositoryGenerator_WithDiRegistration_UpdatesDi()
    {
        using var tmp = new TempProject();
        tmp.AddFile("Infrastructure/DependencyInjection.cs", """
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s) { return s; }
}
""");
        var tmpConfig = GeneratorConfig.ForTargetRoot(tmp.Root);
        var gen = new RepositoryGenerator(tmpConfig, Renderer, new());
        var plan = gen.CreatePlan(new()
        {
            EntityName = "HRepo", AddDependencyInjectionRegistration = true,
        });
        Assert.Contains(plan.Operations, o => o.Kind == GenerationOperationKind.UpdateFile);
    }

    [Fact]
    public void RepositoryGenerator_NoDi_NoDiUpdate()
    {
        var gen = new RepositoryGenerator(Config, Renderer, new());
        var plan = gen.CreatePlan(new()
        {
            EntityName = "Foo", AddDependencyInjectionRegistration = false,
        });
        Assert.DoesNotContain(plan.Operations, o => o.Kind == GenerationOperationKind.UpdateFile);
    }

    [Fact]
    public void AddRepositoryScenarioWorkflow_WithCustomEntity_ComposesEntityAndRepository()
    {
        var workflow = new CoreWorkflowFactory().AddRepositoryScenario(Config);
        var plan = workflow.CreatePlan(new AddRepositoryScenarioWorkflowRequest(
            "User",
            "Domain.Entities.Admin",
            CreateEntity: true,
            EntitySubfolder: "Admin",
            EntityProperties: [new PropertySpec("int", "Id")],
            GenerateEntityFactoryMethod: true,
            GenerateEntityEfMapping: false,
            GenerateEntityInterface: false,
            EntityDomainMethods: [],
            EntityEfMappingFields: null,
            AddDependencyInjectionRegistration: false,
            Methods: [RepositoryMethodCatalog.Create(RepositoryMethodCatalog.GetById.Key, "User")]));

        Assert.Contains(plan.Files, file => file.Path.EndsWith(Path.Combine("Domain", "Entities", "Admin", "User.cs")));
        Assert.Contains(plan.Files, file => file.Path.EndsWith("IUserRepository.cs"));
        Assert.Contains(plan.Files, file => file.Path.EndsWith("UserRepository.cs"));
    }

    [Fact]
    public void AddCommandScenarioWorkflow_WithGeneratedRepository_ComposesRepositoryBeforeCommand()
    {
        var workflow = new CoreWorkflowFactory().AddCommandScenario(Config);
        var plan = workflow.CreatePlan(new AddCommandScenarioWorkflowRequest(
            new AddCommandWorkflowRequest(
                "Users",
                "CreateUser",
                null,
                [],
                [new CommandHandlerDependency("IUserRepository", "userRepository")],
                true),
            [
                new AddRepositoryScenarioWorkflowRequest(
                    "User",
                    "Domain.Entities",
                    CreateEntity: false,
                    EntitySubfolder: null,
                    EntityProperties: [],
                    GenerateEntityFactoryMethod: false,
                    GenerateEntityEfMapping: false,
                    GenerateEntityInterface: false,
                    EntityDomainMethods: [],
                    EntityEfMappingFields: null,
                    AddDependencyInjectionRegistration: false,
                    Methods: [])
            ]));

        Assert.Contains(plan.Files, file => file.Path.EndsWith("IUserRepository.cs"));
        Assert.Contains(plan.Files, file => file.Path.EndsWith("CreateUserCommand.cs"));
        Assert.Contains(plan.Files, file => file.Path.EndsWith("CreateUserHandler.cs"));
    }

    // ═══ FeatureStructureGenerator (3) ═══

    [Fact]
    public void FeatureStructureGenerator_NewFeature_JustCreatesDirectory()
    {
        var gen = new FeatureStructureGenerator(Config);
        var plan = gen.CreatePlan(new()
        {
            FeaturePath = "New",
        });
        Assert.Contains(plan.Operations, o => o.Kind == GenerationOperationKind.CreateDirectory);
    }

    [Fact]
    public void FeatureStructureGenerator_ExistingFeature_AddsConflict()
    {
        using var tmp = new TempProject();
        var root = Path.Combine(tmp.Root, "Application", "Features", "Test");
        Directory.CreateDirectory(root);
        var tmpConfig = GeneratorConfig.ForTargetRoot(tmp.Root);
        var gen = new FeatureStructureGenerator(tmpConfig);
        var plan = gen.CreatePlan(new()
        {
            FeaturePath = "Test",
        });
        Assert.NotEmpty(plan.Conflicts);
        Assert.Contains("существует", plan.Conflicts[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPathEndsWith(string actual, params string[] expectedSegments)
    {
        var expected = Path.Combine(expectedSegments);
        Assert.EndsWith(expected, actual);
    }
}
