using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Tests;

public class DiscoveryTests
{
    private static string Qry(string name, string @namespace) =>
        @$"namespace {@namespace}; class {name} : IQuery<SomeDto> {{ }}";

    private static string Cmd(string name, string @namespace) =>
        @$"namespace {@namespace}; class {name} : ICommand {{ }}";

    private static string Iface(string name, string @namespace) =>
        @$"namespace {@namespace}; interface {name} {{ }}";

    private static string Cls(string name, string @namespace) =>
        @$"namespace {@namespace}; class {name} {{ }}";

    private static string Di(params string[] registrations) =>
        string.Join("\n", registrations.Select(r =>
            $"services.{r};"));

    // ── Discover: empty ──

    [Fact]
    public void Discover_EmptyProject_ReturnsEmptyModel()
    {
        using var p = new TempProject();
        var model = p.CreateDiscovery().Discover();
        Assert.Empty(model.Features);
        Assert.Empty(model.Dtos);
        Assert.Empty(model.Queries);
        Assert.Empty(model.Commands);
        Assert.Empty(model.QueryServices);
        Assert.Empty(model.Repositories);
        Assert.Empty(model.WebFeatures);
        Assert.Empty(model.DependencyInjection.Registrations);
    }

    [Fact]
    public void Discover_FeaturesDirMissing_ReturnsEmptyFeatures()
    {
        using var p = new TempProject();
        var model = p.CreateDiscovery().Discover();
        Assert.Empty(model.Features);
    }

    // ── Discover: features ──

    [Fact]
    public void Discover_FeatureWithDto_DetectsDto()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs");
        p.AddFile("Application/Features/Test/DTOs/UserDto.cs", "namespace X; class UserDto {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Features);
        Assert.Single(model.Dtos);
        Assert.Equal("UserDto", model.Dtos[0].Name);
        Assert.Equal("Test", model.Dtos[0].FeaturePath);
        Assert.Equal(DtoLocationKind.SharedFeatureDto, model.Dtos[0].LocationKind);
    }

    [Fact]
    public void Discover_LocalQueryDto_DetectsOwnerAndIgnoresNestedFiles()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Queries/GetUsers");
        p.AddFile("Application/Features/Test/Queries/GetUsers/UserDto.cs", "namespace Application.Features.Test.Queries.GetUsers; class UserDto {}");
        p.AddFile("Application/Features/Test/Queries/GetUsers/Models/IgnoredDto.cs", "namespace Application.Features.Test.Queries.GetUsers.Models; class IgnoredDto {}");

        var model = p.CreateDiscovery().Discover();

        var dto = Assert.Single(model.Dtos);
        Assert.Equal("UserDto", dto.Name);
        Assert.Equal(DtoLocationKind.LocalQueryDto, dto.LocationKind);
        Assert.Equal("GetUsers", dto.OwnerQueryName);
        Assert.Equal("GetUsers/UserDto", dto.DisplayName);
    }

    [Fact]
    public void Discover_FeatureWithQuery_DetectsQuery()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Queries/GetUsers");
        p.AddFile("Application/Features/Test/Queries/GetUsers/GetUsersQuery.cs",
            Qry("GetUsersQuery", "Application.Features.Test.Queries.GetUsers"));

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Queries);
        Assert.Equal("GetUsersQuery", model.Queries[0].Name);
    }

    [Fact]
    public void Discover_FeatureWithCommand_DetectsCommand()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Commands/CreateUser");
        p.AddFile("Application/Features/Test/Commands/CreateUser/CreateUserCommand.cs",
            Cmd("CreateUserCommand", "Application.Features.Test.Commands.CreateUser"));

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Commands);
        Assert.Equal("CreateUserCommand", model.Commands[0].Name);
    }

    [Fact]
    public void Discover_FeatureWithQueryServiceInterface_FindsIt()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Interfaces");
        p.AddFile("Application/Features/Test/Interfaces/ITestQueryService.cs",
            Iface("ITestQueryService", "Application.Features.Test.Interfaces"));

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.QueryServices);
        Assert.Equal("ITestQueryService", model.QueryServices[0].InterfaceName);
    }

    [Fact]
    public void Discover_QueryServiceInterface_WithoutImplementation_ImplementationNameIsNull()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Interfaces");
        p.AddFile("Application/Features/Test/Interfaces/ITestQueryService.cs",
            Iface("ITestQueryService", "Application.Features.Test.Interfaces"));

        var model = p.CreateDiscovery().Discover();
        Assert.Null(model.QueryServices[0].ImplementationName);
        Assert.Null(model.QueryServices[0].ImplementationPath);
    }

    [Fact]
    public void Discover_QueryServiceImpl_Exists_Detected()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Interfaces");
        p.AddFile("Application/Features/Test/Interfaces/ITestQueryService.cs",
            Iface("ITestQueryService", "Application.Features.Test.Interfaces"));
        p.AddDir("Infrastructure/Data/QueryServices");
        p.AddFile("Infrastructure/Data/QueryServices/TestQueryService.cs",
            Cls("TestQueryService", "Infrastructure.Data.QueryServices"));

        var model = p.CreateDiscovery().Discover();
        Assert.Equal("TestQueryService", model.QueryServices[0].ImplementationName);
        Assert.NotNull(model.QueryServices[0].ImplementationPath);
        Assert.Equal(QueryServiceImplementationPlacement.CanonicalFeaturePath, model.QueryServices[0].ImplementationPlacement);
    }

    [Fact]
    public void Discover_QueryServiceImpl_ForNestedFeature_InParentSubfolder_IsCanonical()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Document/GroupsAccess/Interfaces");
        p.AddFile("Application/Features/Document/GroupsAccess/Interfaces/IGroupsAccessQueryService.cs",
            Iface("IGroupsAccessQueryService", "Application.Features.Document.GroupsAccess.Interfaces"));
        p.AddDir("Infrastructure/Data/QueryServices/Document");
        p.AddFile("Infrastructure/Data/QueryServices/Document/GroupsAccessQueryService.cs",
            Cls("GroupsAccessQueryService", "Infrastructure.Data.QueryServices.Document"));

        var model = p.CreateDiscovery().Discover();
        var service = Assert.Single(model.QueryServices);
        Assert.Equal(QueryServiceImplementationPlacement.CanonicalFeaturePath, service.ImplementationPlacement);
        Assert.NotNull(service.ImplementationPath);
        Assert.EndsWith(Path.Combine("Infrastructure", "Data", "QueryServices", "Document", "GroupsAccessQueryService.cs"), service.ImplementationPath);
        Assert.Equal(1, service.ImplementationCandidateCount);
    }

    [Fact]
    public void Discover_QueryServiceImpl_InUnexpectedSubfolder_IsMarkedNonCanonical()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Interfaces");
        p.AddFile("Application/Features/Test/Interfaces/ITestQueryService.cs",
            Iface("ITestQueryService", "Application.Features.Test.Interfaces"));
        p.AddDir("Infrastructure/Data/QueryServices/Legacy");
        p.AddFile("Infrastructure/Data/QueryServices/Legacy/TestQueryService.cs",
            Cls("TestQueryService", "Infrastructure.Data.QueryServices.Legacy"));

        var model = p.CreateDiscovery().Discover();
        var service = Assert.Single(model.QueryServices);
        Assert.Equal(QueryServiceImplementationPlacement.NonCanonical, service.ImplementationPlacement);
        Assert.NotNull(service.ImplementationPath);
        Assert.Equal(1, service.ImplementationCandidateCount);
    }

    [Fact]
    public void Discover_QueryServiceImpl_AmbiguousNestedCandidates_AreBlocked()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Interfaces");
        p.AddFile("Application/Features/Test/Interfaces/ITestQueryService.cs",
            Iface("ITestQueryService", "Application.Features.Test.Interfaces"));
        p.AddDir("Infrastructure/Data/QueryServices/A");
        p.AddDir("Infrastructure/Data/QueryServices/B");
        p.AddFile("Infrastructure/Data/QueryServices/A/TestQueryService.cs",
            Cls("TestQueryService", "Infrastructure.Data.QueryServices.A"));
        p.AddFile("Infrastructure/Data/QueryServices/B/TestQueryService.cs",
            Cls("TestQueryService", "Infrastructure.Data.QueryServices.B"));

        var model = p.CreateDiscovery().Discover();
        var service = Assert.Single(model.QueryServices);
        Assert.Equal(QueryServiceImplementationPlacement.Ambiguous, service.ImplementationPlacement);
        Assert.Null(service.ImplementationPath);
        Assert.Equal(2, service.ImplementationCandidateCount);
    }

    [Fact]
    public void Discover_QueryServiceImpl_DuplicateCandidates_AreAmbiguousEvenWhenOneMatchesExpectedPath()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Interfaces");
        p.AddFile("Application/Features/Test/Interfaces/ITestQueryService.cs",
            Iface("ITestQueryService", "Application.Features.Test.Interfaces"));
        p.AddDir("Infrastructure/Data/QueryServices");
        p.AddDir("Infrastructure/Data/QueryServices/Legacy");
        p.AddFile("Infrastructure/Data/QueryServices/TestQueryService.cs",
            Cls("TestQueryService", "Infrastructure.Data.QueryServices"));
        p.AddFile("Infrastructure/Data/QueryServices/Legacy/TestQueryService.cs",
            Cls("TestQueryService", "Infrastructure.Data.QueryServices.Legacy"));

        var model = p.CreateDiscovery().Discover();
        var service = Assert.Single(model.QueryServices);
        Assert.Equal(QueryServiceImplementationPlacement.Ambiguous, service.ImplementationPlacement);
        Assert.Null(service.ImplementationPath);
        Assert.Equal(2, service.ImplementationCandidateCount);
    }

    [Fact]
    public void Discover_DiFileExists_ParsesRegistrations()
    {
        using var p = new TempProject();
        p.AddFile("Infrastructure/DependencyInjection.cs",
            Di("AddScoped<IFoo, Foo>()", "AddScoped<IBar, Bar>()", "AddTransient<IQux, Qux>()"));

        var model = p.CreateDiscovery().Discover();
        Assert.Equal(3, model.DependencyInjection.Registrations.Count);

        var regs = model.DependencyInjection.Registrations;
        Assert.Contains(regs, r => r.ServiceType == "IFoo" && r.ImplementationType == "Foo" && r.Lifetime == "Scoped");
        Assert.Contains(regs, r => r.ServiceType == "IBar" && r.ImplementationType == "Bar" && r.Lifetime == "Scoped");
        Assert.Contains(regs, r => r.ServiceType == "IQux" && r.ImplementationType == "Qux" && r.Lifetime == "Transient");
    }

    [Fact]
    public void Discover_DiFileMissing_ReturnsEmptyRegistrations()
    {
        using var p = new TempProject();
        var model = p.CreateDiscovery().Discover();
        Assert.Empty(model.DependencyInjection.Registrations);
    }

    // ── Nested features ──

    [Fact]
    public void Discover_NestedFeatures_GroupFolder_DetectsChildren()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Document/ReferenceDocuments/DTOs");
        p.AddFile("Application/Features/Document/ReferenceDocuments/DTOs/DocDto.cs", "namespace X; class DocDto {}");
        p.AddDir("Application/Features/Document/GroupsAccess/DTOs");
        p.AddFile("Application/Features/Document/GroupsAccess/DTOs/GroupDto.cs", "namespace X; class GroupDto {}");

        var model = p.CreateDiscovery().Discover();
        var paths = model.Features.Select(f => f.RelativePath).OrderBy(x => x).ToList();
        Assert.Equal(2, paths.Count);
        Assert.Contains("Document/ReferenceDocuments", paths);
        Assert.Contains("Document/GroupsAccess", paths);
    }

    [Fact]
    public void Discover_NestedFeatures_ThreeLevels()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/A/B/C/DTOs");
        p.AddFile("Application/Features/A/B/C/DTOs/DeepDto.cs", "namespace X; class DeepDto {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Features);
        Assert.Equal("A/B/C", model.Features[0].RelativePath);
    }

    [Fact]
    public void Discover_IgnoresBinObjFolders()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Real/DTOs");
        p.AddDir("Application/Features/bin");       // should be ignored
        p.AddDir("Application/Features/obj");       // should be ignored

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Features);
        Assert.Equal("Real", model.Features[0].RelativePath);
    }

    [Fact]
    public void Discover_FeatureInternals_NotDetectedAsFeatures()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs");
        p.AddDir("Application/Features/Test/Queries/GetUsers/Models");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Features); // only "Test", not "Test/Queries/GetUsers"
    }

    [Fact]
    public void Discover_FeatureWithModelsFolder_IsDetected()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/Models");
        p.AddFile("Application/Features/Test/Models/SomeModel.cs", "namespace X; class SomeModel {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Features);
    }

    // ── DTO in subfolders ──

    [Fact]
    public void Discover_DtoInSubfolder_DisplayNameIncludesFolder()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs/Admin");
        p.AddFile("Application/Features/Test/DTOs/Admin/CategoryDto.cs", "namespace X; class CategoryDto {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Dtos);
        Assert.Equal("CategoryDto", model.Dtos[0].Name);
        Assert.Equal("Admin/CategoryDto", model.Dtos[0].DisplayName);
    }

    [Fact]
    public void Discover_DtoSubfolders_AreExposedInProjectModel()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs/Admin/Internal");
        p.AddFile("Application/Features/Test/DTOs/Admin/Internal/CategoryDto.cs", "namespace X; class CategoryDto {}");

        var model = p.CreateDiscovery().Discover();

        Assert.Contains(model.DtoSubfolders, folder => folder.FeaturePath == "Test" && folder.RelativePath == "Admin");
        Assert.Contains(model.DtoSubfolders, folder => folder.FeaturePath == "Test" && folder.RelativePath == "Admin/Internal");
    }

    [Fact]
    public void Discover_DtoInRoot_DisplayNameIsJustName()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs");
        p.AddFile("Application/Features/Test/DTOs/CategoryDto.cs", "namespace X; class CategoryDto {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Dtos);
        Assert.Equal("CategoryDto", model.Dtos[0].DisplayName);
    }

    [Fact]
    public void Discover_NonDtoFile_NotCountedAsDto()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs");
        p.AddFile("Application/Features/Test/DTOs/SomeHelper.cs", "namespace X; class SomeHelper {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Empty(model.Dtos);
    }

    // ── Namespace parsing ──

    [Fact]
    public void Discover_NamespacePresent_ParsedCorrectly()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs");
        p.AddFile("Application/Features/Test/DTOs/UserDto.cs",
            "namespace Application.Features.Test.DTOs;\npublic class UserDto {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Dtos);
        Assert.Equal("Application.Features.Test.DTOs", model.Dtos[0].Namespace);
    }

    [Fact]
    public void Discover_NoNamespace_ReturnsEmptyString()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs");
        p.AddFile("Application/Features/Test/DTOs/DummyDto.cs",
            "public class DummyDto {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Dtos);
        Assert.Equal("", model.Dtos[0].Namespace);
    }

    // ── Web features ──

    [Fact]
    public void Discover_WebFeatures_Nested()
    {
        using var p = new TempProject();
        p.AddDir("Web/Features/Admin/Users");
        p.AddDir("Web/Features/Home");

        var model = p.CreateDiscovery().Discover();
        var paths = model.WebFeatures.Select(f => f.RelativePath).OrderBy(x => x).ToList();
        Assert.Contains("Admin", paths);
        Assert.Contains("Admin/Users", paths);
        Assert.Contains("Home", paths);
    }

    // ── Repositories ──

    [Fact]
    public void Discover_RepositoryInterface_Detected()
    {
        using var p = new TempProject();
        p.AddDir("Application/Common/Interfaces/Repositories");
        p.AddFile("Application/Common/Interfaces/Repositories/IHApplicationRepository.cs",
            "namespace X; interface IHApplicationRepository {}");

        var model = p.CreateDiscovery().Discover();
        Assert.Single(model.Repositories);
        Assert.Equal("IHApplicationRepository", model.Repositories[0].InterfaceName);
    }

    [Fact]
    public void Discover_RepositoryAndEntity_ExposeHandlerContracts()
    {
        using var p = new TempProject();
        p.AddDir("Application/Common/Interfaces/Repositories");
        p.AddFile("Application/Common/Interfaces/Repositories/IUserRepository.cs", """
using System.Threading;
using System.Threading.Tasks;
namespace Application.Common.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(int id, CancellationToken ct = default);
        Task AddAsync(User entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
""");
        p.AddDir("Domain/Entities");
        p.AddFile("Domain/Entities/User.cs", """
namespace Domain.Entities
{
    public class User
    {
        public static User Create(string name) => new();
        public void Update(string name) { }
        private void Hidden() { }
    }
}
""");

        var model = p.CreateDiscovery().Discover();

        var repository = Assert.Single(model.Repositories);
        Assert.Equal("User", repository.EntityName);
        Assert.Equal(["GetByIdAsync", "AddAsync", "DeleteAsync"], repository.Methods.Select(method => method.Name));

        var entity = Assert.Single(model.Entities);
        Assert.Equal(["Create", "Update"], entity.Methods.Select(method => method.Name));
        Assert.True(entity.Methods.Single(method => method.Name == "Create").IsStatic);
    }

    [Fact]
    public void Discover_NoRepositoriesDir_EmptyList()
    {
        using var p = new TempProject();
        var model = p.CreateDiscovery().Discover();
        Assert.Empty(model.Repositories);
    }

    [Fact]
    public void Discover_Entities_ExposeNamespaceAndRelativePath()
    {
        using var p = new TempProject();
        p.AddDir("Domain/Entities/Admin");
        p.AddFile("Domain/Entities/Admin/User.cs", "namespace Domain.Entities.Admin; public class User {}");

        var model = p.CreateDiscovery().Discover();
        var entity = Assert.Single(model.Entities);
        Assert.Equal("Domain.Entities.Admin", entity.Namespace);
        Assert.Equal("Admin", entity.RelativePath);
        Assert.Equal("Admin/User", entity.DisplayName);
    }

    // ── GetFeaturePaths ──

    [Fact]
    public void GetFeaturePaths_ReturnsRelativePaths()
    {
        using var p = new TempProject();
        p.AddDir("Application/Features/Test/DTOs");
        p.AddDir("Application/Features/Document/Groups/DTOs");

        var paths = p.CreateDiscovery().GetFeaturePaths();
        Assert.Equal(2, paths.Count);
        Assert.Contains("Test", paths);
        Assert.Contains("Document/Groups", paths);
    }
}
