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
        Assert.DoesNotContain("public async Task Handle", handler.Content);
    }

    [Fact]
    public void CommandGenerator_CollectionParameter_AddsCollectionsUsing()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "CreateUsers",
            Properties = [new("IEnumerable<int>", "Ids")],
        });

        var command = plan.Files.Single(file => file.Path.EndsWith("Command.cs"));
        Assert.Contains("using System.Collections.Generic;", command.Content);
        Assert.Contains("public IEnumerable<int> Ids", command.Content);
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

    [Fact]
    public void CommandGenerator_Create_WithExactContracts_GeneratesFactoryAndAdd()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "CreateUser",
            Properties = [new("string", "Name")],
            Dependencies =
            [
                new("IUserRepository", "userRepository"),
                new("ICurrentUserContext", "currentUserContext"),
            ],
            HandlerScaffoldContext = new CommandHandlerScaffoldContext(
            [
                new CommandHandlerRepositoryContract(
                    "IUserRepository",
                    "userRepository",
                    "User",
                    [new("AddAsync", "Task", [new("User", "entity"), new("CancellationToken", "ct")])],
                    new CommandHandlerEntityContract(
                        "User",
                        "Domain.Entities",
                        [new("Create", "User", [new("string", "name"), new("int", "userId")], IsStatic: true)])),
            ]),
        });

        var handler = plan.Files.Single(file => file.Path.EndsWith("Handler.cs"));
        Assert.Contains("public async Task Handle", handler.Content);
        Assert.Contains("var entity = User.Create(request.Name, _currentUserContext.UserId);", handler.Content);
        Assert.Contains("await _userRepository.AddAsync(entity, cancellationToken);", handler.Content);
        Assert.Contains("\n            await _userRepository.AddAsync", handler.Content);
        Assert.DoesNotContain("\n                        await _userRepository.AddAsync", handler.Content);
        Assert.Contains("using Domain.Entities;", handler.Content);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void CommandGenerator_Update_WithExactContracts_GeneratesMethodChain()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "UpdateUser",
            Properties = [new("int", "Id"), new("string", "Name")],
            Dependencies = [new("IUserRepository", "userRepository")],
            HandlerScaffoldContext = new CommandHandlerScaffoldContext(
            [
                new CommandHandlerRepositoryContract(
                    "IUserRepository",
                    "userRepository",
                    "User",
                    [
                        new("GetByIdAsync", "Task<User>", [new("int", "id"), new("CancellationToken", "ct")]),
                        new("UpdateAsync", "Task", [new("User", "entity"), new("CancellationToken", "ct")]),
                    ],
                    new CommandHandlerEntityContract(
                        "User",
                        "Domain.Entities",
                        [new("Update", "void", [new("string", "name")])])),
            ]),
        });

        var handler = plan.Files.Single(file => file.Path.EndsWith("Handler.cs"));
        Assert.Contains("var entity = await _userRepository.GetByIdAsync(request.Id, cancellationToken);", handler.Content);
        Assert.Contains("entity.Update(request.Name);", handler.Content);
        Assert.Contains("await _userRepository.UpdateAsync(entity, cancellationToken);", handler.Content);
        Assert.DoesNotContain("\n                        entity.Update", handler.Content);
        Assert.DoesNotContain("\n                        await _userRepository.UpdateAsync", handler.Content);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void CommandGenerator_Delete_WithExactContract_GeneratesDeleteCall()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "DeleteUser",
            Properties = [new("int", "Id")],
            Dependencies = [new("IUserRepository", "userRepository")],
            HandlerScaffoldContext = new CommandHandlerScaffoldContext(
            [
                new CommandHandlerRepositoryContract(
                    "IUserRepository",
                    "userRepository",
                    "User",
                    [new("DeleteAsync", "Task", [new("int", "id"), new("CancellationToken", "ct")])],
                    null),
            ]),
        });

        var handler = plan.Files.Single(file => file.Path.EndsWith("Handler.cs"));
        Assert.Contains("public async Task Handle", handler.Content);
        Assert.Contains("await _userRepository.DeleteAsync(request.Id, cancellationToken);", handler.Content);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void CommandGenerator_AutomaticBodyWithoutMatch_UsesDescriptiveFallback()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "DeleteUser",
            Properties = [new("int", "Id")],
            Dependencies = [new("IUserRepository", "userRepository")],
        });

        var handler = plan.Files.Single(file => file.Path.EndsWith("Handler.cs"));
        Assert.Contains("throw new NotImplementedException();", handler.Content);
        Assert.DoesNotContain("no selected repository contract is available", handler.Content);
        Assert.Contains("no selected repository contract is available", Assert.Single(plan.Warnings).Message);
    }

    [Fact]
    public void CommandGenerator_AutomaticBodyWithTwoMatchingRepositories_UsesAmbiguityFallback()
    {
        var deleteMethod = new CommandHandlerMethodContract(
            "DeleteAsync",
            "Task",
            [new("int", "id"), new("CancellationToken", "ct")]);
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "DeleteUser",
            Properties = [new("int", "Id")],
            Dependencies =
            [
                new("IUserRepository", "userRepository"),
                new("IArchiveRepository", "archiveRepository"),
            ],
            HandlerScaffoldContext = new CommandHandlerScaffoldContext(
            [
                new("IUserRepository", "userRepository", "User", [deleteMethod], null),
                new("IArchiveRepository", "archiveRepository", "User", [deleteMethod], null),
            ]),
        });

        var handler = plan.Files.Single(file => file.Path.EndsWith("Handler.cs"));
        Assert.Contains("throw new NotImplementedException();", handler.Content);
        Assert.DoesNotContain("more than one Delete method chain", handler.Content);
        Assert.Contains("more than one Delete method chain", Assert.Single(plan.Warnings).Message);
    }

    [Fact]
    public void CommandGenerator_DeleteWithMissingId_ReportsParameterMismatchOnlyInWarning()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "DeleteUser",
            Dependencies = [new("IUserRepository", "userRepository")],
            HandlerScaffoldContext = new CommandHandlerScaffoldContext(
            [
                new(
                    "IUserRepository",
                    "userRepository",
                    "User",
                    [new("DeleteAsync", "Task", [new("int", "id"), new("CancellationToken", "ct")])],
                    null),
            ]),
        });

        var handler = plan.Files.Single(file => file.Path.EndsWith("Handler.cs"));
        Assert.Contains("throw new NotImplementedException();", handler.Content);
        Assert.DoesNotContain("parameters do not match", handler.Content);
        Assert.Contains("DeleteAsync parameters do not match", Assert.Single(plan.Warnings).Message);
    }

    [Fact]
    public void CommandGenerator_DisabledAutomaticBody_UsesPlainStubWithoutWarning()
    {
        var plan = new CommandGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            CommandName = "DeleteUser",
            GenerateHandlerBody = false,
        });

        var handler = plan.Files.Single(file => file.Path.EndsWith("Handler.cs"));
        Assert.Contains("throw new NotImplementedException();", handler.Content);
        Assert.Empty(plan.Warnings);
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
        Assert.Contains("namespace Application.Features.Test.DTOs", file.Content);
        Assert.DoesNotContain("DTOs.Admin", file.Content);
    }

    [Fact]
    public void DtoGenerator_StandardPropertyTypes_AddsRequiredUsings()
    {
        var plan = new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            DtoName = "Dto",
            Properties = [new("List<Guid>", "Ids"), new("DateTime?", "CreatedAt")],
        });

        var file = Assert.Single(plan.Files);
        Assert.Contains("using System;", file.Content);
        Assert.Contains("using System.Collections.Generic;", file.Content);
    }

    [Fact]
    public void DtoGenerator_InvalidPropertyType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DtoGenerator(Config, Renderer).CreatePlan(new()
        {
            FeaturePath = "Test",
            DtoName = "Dto",
            Properties = [new("List<", "Ids")],
        }));
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
            DtoNamespace = "Application.Features.Test.DTOs",
            InitialParameters = [new("int", "TabNumber")],
        });

        Assert.Equal(2, plan.Files.Count);
        var intf = plan.Files.Single(f => f.Path.EndsWith("ITestQueryService.cs"));
        var implementation = plan.Files.Single(f => Path.GetFileName(f.Path) == "TestQueryService.cs");
        Assert.Contains("using System.Collections.Generic;", intf.Content);
        Assert.Contains("using Application.Features.Test.DTOs;", intf.Content);
        Assert.Contains("using System.Collections.Generic;", implementation.Content);
        Assert.Contains("using Application.Features.Test.DTOs;", implementation.Content);
        Assert.Contains(
            "return await ExecuteAsync(x => x.QueryAsync<UserDto>(sql, new { tabNumber }, ct));",
            implementation.Content);
        Assert.DoesNotContain("_connection", implementation.Content);
        Assert.DoesNotContain("@TabNumber", implementation.Content);
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
        var implementation = plan.Files.Single(f => Path.GetFileName(f.Path) == "TestQueryService.cs");
        Assert.Contains(
            "return await ExecuteAsync(x => x.QueryAsync<UserDto>(sql, ct: ct));",
            implementation.Content);
        Assert.DoesNotContain("new {", implementation.Content);
    }

    [Fact]
    public void QueryServiceCreator_WithSingleBody_UsesQueryFirstOrDefaultAsync()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<UserDto>", InitialMethodName = "GetUserAsync",
            GenerateImplementationBody = true, DtoTypeName = "UserDto",
            InitialParameters = [new("int", "UserId")],
        });

        var implementation = plan.Files.Single(f => Path.GetFileName(f.Path) == "TestQueryService.cs");
        Assert.Contains(
            "return await ExecuteAsync(x => x.QueryFirstOrDefaultAsync<UserDto>(sql, new { userId }, ct));",
            implementation.Content);
    }

    [Fact]
    public void QueryServiceCreator_WithBooleanBody_UsesExecuteScalarOrDefaultAsync()
    {
        var gen = new QueryServiceGenerator(Config, Renderer, new());
        var plan = gen.CreateServicePlan(new()
        {
            FeaturePath = "Test", InterfaceName = "ITestQueryService", ImplementationName = "TestQueryService",
            ImplementationPath = GenerationNaming.GetQueryServiceImplementationPath(Config, "Test", "TestQueryService"),
            ImplementationNamespace = GenerationNaming.GetQueryServiceImplementationNamespace(Config, "Test"),
            InitialReturnType = "Task<bool>", InitialMethodName = "ExistsAsync",
            GenerateImplementationBody = true,
            InitialParameters = [new("int", "UserId")],
        });

        var implementation = plan.Files.Single(f => Path.GetFileName(f.Path) == "TestQueryService.cs");
        Assert.Contains(
            "return await ExecuteAsync(x => x.ExecuteScalarOrDefaultAsync<bool>(sql, new { userId }, ct));",
            implementation.Content);
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
        public Task<int> GetAsync(string name, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
""", implementationUpdate.Content);
        Assert.DoesNotContain("\n            public Task<int> GetAsync", implementationUpdate.Content);
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
            DtoNamespace = "Application.Features.Test.DTOs",
            Parameters = [new("int", "OwnerId")],
        });

        Assert.Empty(plan.Conflicts);
        Assert.Equal(2, plan.Operations.Count);
        var interfaceUpdate = Assert.IsType<UpdateFileOperation>(
            plan.Operations.Single(operation => operation.Path == Path.GetFullPath(ifacePath)));
        var implementationUpdate = Assert.IsType<UpdateFileOperation>(
            plan.Operations.Single(operation => operation.Path == Path.GetFullPath(implPath)));
        Assert.Contains("using System.Collections.Generic;", interfaceUpdate.Content);
        Assert.Contains("using Application.Features.Test.DTOs;", interfaceUpdate.Content);
        Assert.Contains("using System.Collections.Generic;", implementationUpdate.Content);
        Assert.Contains("using Application.Features.Test.DTOs;", implementationUpdate.Content);
        Assert.Contains(
            "return await ExecuteAsync(x => x.QueryAsync<SomeDto>(sql, new { ownerId }, ct));",
            implementationUpdate.Content);
        Assert.DoesNotContain("_connection", implementationUpdate.Content);
        Assert.Single(
            interfaceUpdate.Content.Split(["\r\n", "\n"], StringSplitOptions.None),
            line => line == "using System.Collections.Generic;");
        Assert.Single(
            interfaceUpdate.Content.Split(["\r\n", "\n"], StringSplitOptions.None),
            line => line == "using Application.Features.Test.DTOs;");
        Assert.Single(
            implementationUpdate.Content.Split(["\r\n", "\n"], StringSplitOptions.None),
            line => line == "using System.Collections.Generic;");
        Assert.Single(
            implementationUpdate.Content.Split(["\r\n", "\n"], StringSplitOptions.None),
            line => line == "using Application.Features.Test.DTOs;");
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
        Assert.DoesNotContain("public MyEntity()", mapping.Content);
        Assert.Contains("public MyEntity(Entity entity)", mapping.Content);
        Assert.Contains("this.IdAbonent = entity.AbonentId", mapping.Content);
        Assert.Contains("entity.Name = this.Name", mapping.Content);
        Assert.Contains("((IBaseEntity)entity).SetId(this.Id)", mapping.Content);
    }

    [Fact]
    public void EntityGenerator_ExistingMapping_LeavesFileUntouchedAndAddsWarning()
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

        Assert.DoesNotContain(
            plan.Operations,
            operation => operation is UpdateFileOperation && operation.Path.Contains("EntitiesMapping"));
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

    [Fact]
    public void EntityGenerator_GuidProperty_AddsSystemUsing()
    {
        var plan = new EntityGenerator(Config, Renderer).CreatePlan(new()
        {
            EntityName = "Permission",
            Properties = [new("Guid", "ExternalId")],
        });

        var entity = plan.Files.Single(file => file.Path.Contains("Domain"));
        Assert.Contains("using System;", entity.Content);
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
        Assert.Contains("Task DeleteAsync(int id, CancellationToken ct = default);", intf.Content);
        Assert.True(RepositoryMethodCatalog.Delete.IsSelectedByDefault);
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
    public void RepositoryGenerator_CustomCollectionMethod_AddsUsingAndAvoidsAsyncStub()
    {
        var plan = new RepositoryGenerator(Config, Renderer, new()).CreatePlan(new()
        {
            EntityName = "Foo",
            Methods =
            [
                new RepositoryMethodSpec(
                    "FindAsync",
                    "Task<IEnumerable<Foo>>",
                    [new PropertySpec("IReadOnlyList<Guid>", "ids")]),
            ],
        });

        var @interface = plan.Files.Single(file => file.Path.EndsWith("IFooRepository.cs"));
        var implementation = plan.Files.Single(file =>
            file.Path.EndsWith("FooRepository.cs") &&
            !file.Path.EndsWith("IFooRepository.cs"));
        Assert.Contains("using System;", @interface.Content);
        Assert.Contains("using System.Collections.Generic;", @interface.Content);
        Assert.Contains("public Task<IEnumerable<Foo>> FindAsync", implementation.Content);
        Assert.DoesNotContain("public async Task<IEnumerable<Foo>>", implementation.Content);
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
                [],
                [new CommandHandlerDependency("IUserRepository", "userRepository")],
                null),
            [
                new AddRepositoryScenarioWorkflowRequest(
                    "User",
                    "Domain.Entities",
                    CreateEntity: false,
                    EntitySubfolder: null,
                    EntityProperties: [],
                    GenerateEntityFactoryMethod: false,
                    GenerateEntityEfMapping: false,
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
