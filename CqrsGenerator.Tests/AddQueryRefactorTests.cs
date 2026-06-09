using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Core.Validation.Rules;
using CqrsGenerator.Core.Workflows;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Tests;

public class AddQueryRefactorTests
{
    [Fact]
    public void AddQueryRequestBuilder_UsesExistingDtoSelection()
    {
        var builder = new AddQueryRequestBuilder();
        var result = builder.Build(new AddQueryFormState(
            "Users",
            "Users",
            "GetUsers",
            CreateSharedSelection("UserDto"),
            null,
            ResponseShape.Single,
            [new PropertySpec("int", "id")],
            new QueryServiceSuggestion("IUsersQueryService", "UsersQueryService", null, null, QueryServiceSuggestionMode.CreateNew, AutoItemStatus.Created),
            true,
            "GetUsersAsync",
            true,
            true,
            true));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Request);
        var dtoSelection = Assert.IsType<UseSharedFeatureDtoSelection>(result.Request!.DtoSelection);
        Assert.Equal("UserDto", dtoSelection.DtoName);
        Assert.NotNull(result.Request.QueryService);
        Assert.True(result.Request.QueryService!.CreateNew);
    }

    [Fact]
    public void AddQueryRequestBuilder_UsesCustomDtoFlow()
    {
        var builder = new AddQueryRequestBuilder();

        var result = builder.Build(new AddQueryFormState(
            "Users",
            "Users",
            "GetUsers",
            CreateLocalSelection("UserLookupDto", "GetUsers"),
            null,
            ResponseShape.List,
            [],
            new QueryServiceSuggestion("IUsersQueryService", "UsersQueryService", "iface.cs", "impl.cs", QueryServiceSuggestionMode.UpdateExisting, AutoItemStatus.Modified),
            true,
            "GetUsersAsync",
            false,
            false,
            false));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Request);
        var dtoSelection = Assert.IsType<CreateLocalQueryDtoSelection>(result.Request!.DtoSelection);
        Assert.Empty(dtoSelection.Properties);
        Assert.Equal(ResponseShape.List, result.Request.ResponseShape);
        Assert.False(result.Request.QueryService!.CreateNew);
        Assert.Equal("Task<List<UserLookupDto>>", result.Request.QueryService.ReturnType);
    }

    [Fact]
    public void AddQueryRequestBuilder_UsesFreeFormResultTypeWithoutDtoMetadata()
    {
        var builder = new AddQueryRequestBuilder();

        var result = builder.Build(new AddQueryFormState(
            "Users",
            "Users",
            "GetUsers",
            new QueryDtoSelectionState(
                "ExternalResult",
                null,
                null,
                DtoLocationKind.SharedFeatureDto,
                null,
                CreateNewLocalDto: false,
                IsSelectable: true,
                SelectionBlockedReason: null),
            null,
            ResponseShape.Single,
            [],
            new QueryServiceSuggestion("IUsersQueryService", "UsersQueryService", null, null, QueryServiceSuggestionMode.CreateNew, AutoItemStatus.Created),
            true,
            "GetUsersAsync",
            true,
            true,
            true));

        Assert.True(result.Succeeded);
        var dtoSelection = Assert.IsType<UseSharedFeatureDtoSelection>(result.Request!.DtoSelection);
        Assert.Equal("ExternalResult", dtoSelection.DtoName);
        Assert.Equal(string.Empty, dtoSelection.DtoNamespace);
        Assert.Equal(string.Empty, dtoSelection.DtoPath);
    }

    [Theory]
    [InlineData(ResponseShape.Single, "Task<UserDto>")]
    [InlineData(ResponseShape.List, "Task<List<UserDto>>")]
    [InlineData(ResponseShape.Enumerable, "Task<IEnumerable<UserDto>>")]
    public void AddQueryRequestBuilder_MapsResponseShape(ResponseShape responseShape, string expectedReturnType)
    {
        var builder = new AddQueryRequestBuilder();
        var result = builder.Build(new AddQueryFormState(
            "Users",
            "Users",
            "GetUsers",
            CreateSharedSelection("UserDto"),
            null,
            responseShape,
            [],
            new QueryServiceSuggestion("IUsersQueryService", "UsersQueryService", null, null, QueryServiceSuggestionMode.CreateNew, AutoItemStatus.Created),
            true,
            "GetUsersAsync",
            true,
            true,
            true));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedReturnType, result.Request!.QueryService!.ReturnType);
    }

    [Fact]
    public void AddQueryRequestBuilder_SplitsHandlerAndQueryServiceBodyFlags()
    {
        var builder = new AddQueryRequestBuilder();
        var result = builder.Build(new AddQueryFormState(
            "Users",
            "Users",
            "GetUsers",
            CreateSharedSelection("UserDto"),
            null,
            ResponseShape.Single,
            [],
            new QueryServiceSuggestion("IUsersQueryService", "UsersQueryService", null, null, QueryServiceSuggestionMode.CreateNew, AutoItemStatus.Created),
            true,
            "GetUsersAsync",
            false,
            true,
            true));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Request);
        Assert.False(result.Request!.GenerateHandlerBody);
        Assert.NotNull(result.Request.QueryService);
        Assert.True(result.Request.QueryService!.GenerateImplementationBody);
    }

    [Fact]
    public void AddQueryRequestBuilder_DisabledQueryServiceMethod_SkipsQueryServiceWorkflow()
    {
        var builder = new AddQueryRequestBuilder();
        var result = builder.Build(new AddQueryFormState(
            "Users",
            "Users",
            "GetUsers",
            CreateSharedSelection("UserDto"),
            null,
            ResponseShape.Single,
            [],
            new QueryServiceSuggestion("IUsersQueryService", "UsersQueryService", null, null, QueryServiceSuggestionMode.CreateNew, AutoItemStatus.Created),
            false,
            string.Empty,
            true,
            true,
            true));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Request);
        Assert.Null(result.Request!.QueryService);
    }

    [Fact]
    public void QueryServiceSuggestionService_ReturnsDiscoveredService()
    {
        using var tmp = new TempProject();
        var service = new QueryServiceSuggestionService();
        var model = CreateProjectModel(
            tmp.Root,
            [
                new QueryServiceInfo(
                    "IUsersQueryService",
                    "Users",
                    Path.Combine(tmp.Root, "Application", "Features", "Users", "Interfaces", "IUsersQueryService.cs"),
                    "UsersQueryService",
                    Path.Combine(tmp.Root, "Infrastructure", "Data", "QueryServices", "UsersQueryService.cs"),
                    QueryServiceImplementationPlacement.CanonicalFeaturePath,
                    1),
            ]);

        var suggestion = service.Suggest(model, "Users", "Users");

        Assert.NotNull(suggestion);
        Assert.Equal(QueryServiceSuggestionMode.UpdateExisting, suggestion!.Mode);
        Assert.Equal(AutoItemStatus.Modified, suggestion.Status);
        Assert.Equal("UsersQueryService", suggestion.ImplementationName);
    }

    [Fact]
    public void QueryServiceSuggestionService_ReturnsCreateSuggestionWhenMissing()
    {
        var service = new QueryServiceSuggestionService();

        var model = CreateProjectModel(Path.GetTempPath(), []);

        var suggestion = service.Suggest(model, "Users", "Users");

        Assert.NotNull(suggestion);
        Assert.Equal(QueryServiceSuggestionMode.CreateNew, suggestion!.Mode);
        Assert.Equal("IUsersQueryService", suggestion.InterfaceName);
        Assert.Equal("UsersQueryService", suggestion.ImplementationName);
        Assert.Equal(AutoItemStatus.Created, suggestion.Status);
    }

    [Fact]
    public void QueryServiceSuggestionService_BlocksMissingImplementation()
    {
        using var tmp = new TempProject();
        var service = new QueryServiceSuggestionService();
        var model = CreateProjectModel(
            tmp.Root,
            [
                new QueryServiceInfo("IUsersQueryService", "Users", "iface.cs", null, null, QueryServiceImplementationPlacement.Missing, 0),
            ]);

        var suggestion = service.Suggest(model, "Users", "Users");

        Assert.NotNull(suggestion);
        Assert.Equal(QueryServiceSuggestionMode.Blocked, suggestion!.Mode);
        Assert.Null(suggestion.ImplementationPath);
    }

    [Fact]
    public void AddQueryRequestBuilder_BlockedQueryServiceSuggestion_Fails()
    {
        var builder = new AddQueryRequestBuilder();
        var result = builder.Build(new AddQueryFormState(
            "Users",
            "Users",
            "GetUsers",
            CreateSharedSelection("UserDto"),
            null,
            ResponseShape.Single,
            [],
            new QueryServiceSuggestion("IUsersQueryService", "UsersQueryService", "iface.cs", null, QueryServiceSuggestionMode.Blocked, AutoItemStatus.Modified),
            true,
            "GetUsersAsync",
            true,
            true,
            true));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("could not be resolved automatically", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultArchitectureRuleCatalog_ContainsDefaultRules()
    {
        var rules = DefaultArchitectureRuleCatalog.CreateRules();
        var project = CreateProjectModel(
            Path.GetTempPath(),
            [
                new QueryServiceInfo("IUsersQueryService", "Users", "iface1.cs", "UsersQueryService", "impl1.cs"),
                new QueryServiceInfo("IUsersQueryServiceAlt", "Users", "iface2.cs", "UsersQueryServiceAlt", "impl2.cs"),
            ]);

        Assert.Contains(rules, rule => rule is SingleQueryServicePerFeatureRule);
        Assert.Contains(rules, rule => rule is QueryServiceImplementationPlacementRule);
        Assert.NotEmpty(DefaultArchitectureRuleCatalog.CreateRuleSet().CheckAll(project, GeneratorConfig.ForTargetRoot(Path.GetTempPath())));
    }

    [Fact]
    public void DefaultArchitectureRuleCatalog_EmitsOnlyAmbiguousQueryServicePlacementWarnings()
    {
        var warnings = DefaultArchitectureRuleCatalog.CreateRuleSet().CheckAll(
            CreateProjectModel(
                Path.GetTempPath(),
                [
                    new QueryServiceInfo("IUsersQueryService", "Users", "iface.cs", "UsersQueryService", "impl.cs", QueryServiceImplementationPlacement.NonCanonical, 1),
                    new QueryServiceInfo("IUsersQueryServiceAlt", "Users", "iface2.cs", null, null, QueryServiceImplementationPlacement.Ambiguous, 2),
                ]),
            GeneratorConfig.ForTargetRoot(Path.GetTempPath()));

        Assert.Contains(warnings, warning => warning.Code == "CQRS-003");
        Assert.DoesNotContain(warnings, warning => warning.Message.Contains("legacy", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectModel CreateProjectModel(string rootPath, IReadOnlyList<QueryServiceInfo> queryServices)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var applicationPath = Path.Combine(fullRootPath, "Application");
        var infrastructurePath = Path.Combine(fullRootPath, "Infrastructure");
        var webPath = Path.Combine(fullRootPath, "Web");

        return new ProjectModel
        {
            Paths = new ProjectPaths(
                fullRootPath,
                applicationPath,
                Path.Combine(applicationPath, "Features"),
                infrastructurePath,
                Path.Combine(infrastructurePath, "Data", "QueryServices"),
                Path.Combine(infrastructurePath, "Data", "Repositories"),
                Path.Combine(infrastructurePath, "DependencyInjection.cs"),
                webPath,
                Path.Combine(webPath, "Features")),
            Features =
            [
                new FeatureInfo("Users", "Users", Path.Combine(applicationPath, "Features", "Users")),
            ],
            QueryServices = queryServices,
            DependencyInjection = new DependencyInjectionInfo(Path.Combine(infrastructurePath, "DependencyInjection.cs"), []),
        };
    }

    private static QueryDtoSelectionState CreateSharedSelection(string dtoName) =>
        new(
            dtoName,
            "Application.Features.Users.DTOs",
            $"/tmp/{dtoName}.cs",
            DtoLocationKind.SharedFeatureDto,
            null,
            CreateNewLocalDto: false,
            IsSelectable: true,
            SelectionBlockedReason: null);

    private static QueryDtoSelectionState CreateLocalSelection(string dtoName, string ownerQueryName) =>
        new(
            dtoName,
            $"Application.Features.Users.Queries.{ownerQueryName}",
            $"/tmp/{ownerQueryName}/{dtoName}.cs",
            DtoLocationKind.LocalQueryDto,
            ownerQueryName,
            CreateNewLocalDto: true,
            IsSelectable: true,
            SelectionBlockedReason: null);
}
