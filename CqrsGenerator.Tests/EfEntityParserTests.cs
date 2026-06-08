using CqrsGenerator.Core.Discovery;

namespace CqrsGenerator.Tests;

public class EfEntityParserTests
{
    [Fact]
    public void Parse_ValidEntity_ExtractsScalarProperties()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "HApplication.cs");
        File.WriteAllText(path, """
using System.Collections.Generic;

namespace Infrastructure.Data.Entities
{
    public partial class HApplication
    {
        public HApplication()
        {
            HPersonalDataPermission = new HashSet<HPersonalDataPermission>();
        }

        public int Id { get; set; }
        public int IdAbonent { get; set; }
        public int Detail { get; set; }
        public string Name { get; set; }
        public DateTime SignDate { get; set; }
        public int? IdDopParamReason { get; set; }

        public virtual Abonent IdAbonentNavigation { get; set; }
        public virtual ICollection<HPersonalDataPermission> HPersonalDataPermission { get; set; }
    }
}
""");

        var props = EfEntityParser.Parse(path);

        Assert.Equal(6, props.Count);
        Assert.Contains(props, p => p is { EfName: "Id", EfType: "int" });
        Assert.Contains(props, p => p is { EfName: "IdAbonent", EfType: "int" });
        Assert.Contains(props, p => p is { EfName: "Detail", EfType: "int" });
        Assert.Contains(props, p => p is { EfName: "Name", EfType: "string" });
        Assert.Contains(props, p => p is { EfName: "SignDate", EfType: "DateTime" });
        Assert.Contains(props, p => p is { EfName: "IdDopParamReason", EfType: "int?" });
        Assert.DoesNotContain(props, p => p.EfName == "IdAbonentNavigation");
        Assert.DoesNotContain(props, p => p.EfName == "HPersonalDataPermission");
    }

    [Fact]
    public void Parse_FileNotFound_ReturnsEmpty()
    {
        var props = EfEntityParser.Parse("C:\\nonexistent\\file.cs");
        Assert.Empty(props);
    }

    [Fact]
    public void Parse_InvalidCSharp_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "bad.cs");
        File.WriteAllText(path, "{{{ broken syntax @@@ }}}");

        var props = EfEntityParser.Parse(path);
        Assert.Empty(props);
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "empty.cs");
        File.WriteAllText(path, "");

        var props = EfEntityParser.Parse(path);
        Assert.Empty(props);
    }

    [Fact]
    public void Parse_OnlyVirtualProperties_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "X.cs");
        File.WriteAllText(path, """
public class X
{
    public virtual Abonent Nav { get; set; }
    public virtual ICollection<Child> Children { get; set; }
    public virtual List<int> Items { get; set; }
}
""");

        var props = EfEntityParser.Parse(path);
        Assert.Empty(props);
    }

    [Theory]
    [InlineData("Id", "Id")]
    [InlineData("IdAbonent", "AbonentId")]
    [InlineData("SignDate", "SignDate")]
    [InlineData("IdDopParamReason", "DopParamReasonId")]
    [InlineData("IdApplicationType", "ApplicationTypeId")]
    [InlineData("Detail", "Detail")]
    public void ToDomainName_ConvertsCorrectly(string efName, string expected)
    {
        Assert.Equal(expected, EfEntityParser.ToDomainName(efName));
    }
}
