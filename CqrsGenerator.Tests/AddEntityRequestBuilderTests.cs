using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.Models;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Tests;

public class AddEntityRequestBuilderTests
{
    [Fact]
    public void Build_FromEfEntity_ComposesPreparedAndManualProperties()
    {
        var builder = new AddEntityRequestBuilder(new EfEntityPreparationService());
        var candidate = new EfEntityCandidate(
            "HApplication",
            "/tmp/HApplication.cs",
            [
                new EfEntityProperty("IdAbonent", "Int32"),
                new EfEntityProperty("Name", "String"),
            ]);

        var result = builder.Build(new AddEntityFormState(
            EntitySourceMode.EfEntity,
            candidate,
            ["IdAbonent"],
            RenameEfIdentifierProperties: true,
            EntityName: "Application",
            Subfolder: "Admin",
            ManualProperties: [new PropertySpec("DateTime", "CreatedAt")],
            GenerateFactoryMethod: true,
            GenerateEfMapping: true,
            GenerateInterface: true,
            DomainMethods: ["Archive"]));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Request);
        Assert.Equal("Application", result.Request!.EntityName);
        Assert.Equal("Admin", result.Request.SubFolder);
        Assert.True(result.Request.GenerateInterface);
        Assert.Contains(result.Request.Properties, property => property.Name == "AbonentId" && property.Type == "int");
        Assert.Contains(result.Request.Properties, property => property.Name == "CreatedAt" && property.Type == "DateTime");
        Assert.Contains(result.Request.DomainMethods, method => method == "Archive");
        Assert.NotNull(result.Request.EfMappingFields);
        Assert.Contains(result.Request.EfMappingFields!, mapping => mapping == ("AbonentId", "IdAbonent"));
    }
}
