using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Tests;

public class EfEntityPreparationServiceTests
{
    [Fact]
    public void Discover_ReturnsEfEntityCandidates()
    {
        using var project = new TempProject();
        project.AddDir("Infrastructure/Data/Entities");
        project.AddFile("Infrastructure/Data/Entities/HApplication.cs", """
namespace Infrastructure.Data.Entities;
public class HApplication
{
    public int Id { get; set; }
    public int IdAbonent { get; set; }
}
""");

        var service = new EfEntityPreparationService();
        var config = GeneratorConfig.ForTargetRoot(project.Root);

        var candidates = service.Discover(config);

        var candidate = Assert.Single(candidates);
        Assert.Equal("HApplication", candidate.Name);
        Assert.Equal(2, candidate.Properties.Count);
    }

    [Fact]
    public void Prepare_RenamesIdentifierFieldsAndMapsTypes()
    {
        var service = new EfEntityPreparationService();
        var candidate = new EfEntityCandidate(
            "HApplication",
            "/tmp/HApplication.cs",
            [
                new EfEntityProperty("IdAbonent", "Int32"),
                new EfEntityProperty("Name", "String"),
            ]);

        var prepared = service.Prepare(candidate, ["IdAbonent", "Name"], renameIdentifierProperties: true);

        Assert.Collection(prepared.Properties,
            property =>
            {
                Assert.Equal("int", property.Type);
                Assert.Equal("AbonentId", property.Name);
            },
            property =>
            {
                Assert.Equal("string", property.Type);
                Assert.Equal("Name", property.Name);
            });
        Assert.Contains(prepared.EfMappingFields, mapping => mapping == ("AbonentId", "IdAbonent"));
    }
}
