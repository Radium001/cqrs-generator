using CqrsGenerator.Core.Configuration;

namespace CqrsGenerator.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ForTargetRoot_DefaultConventions_CorrectPaths()
    {
        var config = GeneratorConfig.ForTargetRoot("C:\\root");

        Assert.Equal("Application", config.ApplicationPath);
        Assert.Equal("Features", config.FeatureRoot);
        Assert.Equal("DTOs", config.DtoFolderName);
        Assert.Equal("Interfaces", config.InterfacesFolderName);
        Assert.Equal("Queries", config.QueriesFolderName);
        Assert.Equal("Commands", config.CommandsFolderName);
        Assert.Equal("Models", config.ModelsFolderName);
        AssertPathEndsWith(config.ApplicationFeatureRootPath, "Application", "Features");
        AssertPathEndsWith(config.WebFeatureRootPath, "Web", "Features");
        AssertPathEndsWith(config.QueryServicesPath, "Infrastructure", "Data", "QueryServices");
        AssertPathEndsWith(config.RepositoriesPath, "Infrastructure", "Data", "Repositories");
        AssertPathEndsWith(config.DependencyInjectionPath, "Infrastructure", "DependencyInjection.cs");
    }

    [Fact]
    public void ForTargetRoot_CustomConventions_UsesCustom()
    {
        var custom = new ProjectConventions
        {
            ApplicationPath = "App",
            FeatureRoot = "Feats",
            DtoFolderName = "Dtos",
            QueriesFolderName = "Query",
        };

        var config = GeneratorConfig.ForTargetRoot("C:\\root", custom);

        Assert.Equal("App", config.ApplicationPath);
        Assert.Equal("Feats", config.FeatureRoot);
        Assert.Equal("Dtos", config.DtoFolderName);
        Assert.Equal("Query", config.QueriesFolderName);
        AssertPathEndsWith(config.ApplicationFeatureRootPath, "App", "Feats");
    }

    [Fact]
    public void ProjectConventions_Default_HasExpectedValues()
    {
        var c = ProjectConventions.Default;

        Assert.Equal("Application", c.ApplicationRootNamespace);
        Assert.Equal("Infrastructure", c.InfrastructureRootNamespace);
        Assert.Equal("Web", c.WebRootNamespace);
        Assert.Equal("Application", c.ApplicationPath);
        Assert.Equal("Infrastructure", c.InfrastructurePath);
        Assert.Equal("Web", c.WebPath);
        Assert.Equal("Features", c.FeatureRoot);
        Assert.Equal("DTOs", c.DtoFolderName);
        Assert.Equal("Interfaces", c.InterfacesFolderName);
        Assert.Equal("Models", c.ModelsFolderName);
        Assert.Equal("Queries", c.QueriesFolderName);
        Assert.Equal("Commands", c.CommandsFolderName);
        Assert.Equal(Path.Combine("Infrastructure", "Data", "QueryServices"), c.QueryServicesPath);
        Assert.Equal(Path.Combine("Infrastructure", "Data", "Repositories"), c.RepositoriesPath);
        Assert.Equal(Path.Combine("Infrastructure", "DependencyInjection.cs"), c.DependencyInjectionPath);
    }

    [Fact]
    public void ForTargetRoot_ResolvesFullPath()
    {
        var config = GeneratorConfig.ForTargetRoot("test-root");
        Assert.True(Path.IsPathRooted(config.TargetRootPath));
    }

    [Fact]
    public void GeneratorConfig_RootNamespace_ComesFromConventions()
    {
        var config = GeneratorConfig.ForTargetRoot("C:\\x");
        Assert.Equal("Application", config.RootNamespace);
    }

    [Fact]
    public void GeneratorConfig_InfrastructurePath_FromConventions()
    {
        var config = GeneratorConfig.ForTargetRoot("C:\\x");
        Assert.Equal("Infrastructure", config.InfrastructurePath);
        Assert.Equal("Web", config.WebPath);
    }

    private static void AssertPathEndsWith(string actual, params string[] expectedSegments)
    {
        var expected = Path.Combine(expectedSegments);
        Assert.EndsWith(expected, actual);
    }
}
