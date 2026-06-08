using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData("User", true)]
    [InlineData("_private", true)]
    [InlineData("GetUsers", true)]
    [InlineData("Привет", true)]
    [InlineData("a_b", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("class", false)]
    [InlineData("namespace", false)]
    [InlineData("abstract", false)]
    [InlineData("123abc", false)]
    public void IsIdentifier_ReturnsExpected(string value, bool expected)
    {
        Assert.Equal(expected, CSharpNameValidator.IsIdentifier(value));
    }

    [Fact]
    public void IsIdentifier_Null_ReturnsFalse()
    {
        Assert.False(CSharpNameValidator.IsIdentifier(null!));
    }

    [Theory]
    [InlineData("User", true)]
    [InlineData("int", true)]
    [InlineData("string", true)]
    [InlineData("bool", true)]
    [InlineData("decimal", true)]
    [InlineData("double", true)]
    [InlineData("float", true)]
    [InlineData("long", true)]
    [InlineData("object", true)]
    [InlineData("byte", true)]
    [InlineData("char", true)]
    [InlineData("short", true)]
    [InlineData("uint", true)]
    [InlineData("ulong", true)]
    [InlineData("ushort", true)]
    [InlineData("sbyte", true)]
    [InlineData("void", true)]
    [InlineData("class", false)]
    [InlineData("if", false)]
    [InlineData("namespace", false)]
    [InlineData("123abc", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("_private", true)]
    [InlineData("Привет", true)]
    public void IsTypeName_ReturnsExpected(string value, bool expected)
    {
        Assert.Equal(expected, CSharpNameValidator.IsTypeName(value));
    }

    [Fact]
    public void IsTypeName_Null_ReturnsFalse()
    {
        Assert.False(CSharpNameValidator.IsTypeName(null!));
    }

    [Fact]
    public void EnsureTypeName_ValidType_DoesNotThrow()
    {
        var ex = Record.Exception(() => CSharpNameValidator.EnsureTypeName("int", "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureTypeName_KeywordClass_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CSharpNameValidator.EnsureTypeName("class", "test"));
    }

    [Fact]
    public void EnsureIdentifier_ValidName_DoesNotThrow()
    {
        var ex = Record.Exception(() => CSharpNameValidator.EnsureIdentifier("ValidName", "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureIdentifier_Keyword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CSharpNameValidator.EnsureIdentifier("void", "test"));
    }

    [Fact]
    public void EnsureFeaturePath_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CSharpNameValidator.EnsureFeaturePath(""));
    }

    [Fact]
    public void EnsureFeaturePath_Whitespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CSharpNameValidator.EnsureFeaturePath("   "));
    }

    [Fact]
    public void EnsureFeaturePath_DotSegment_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CSharpNameValidator.EnsureFeaturePath("Test/."));
    }

    [Fact]
    public void EnsureFeaturePath_DoubleDotSegment_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CSharpNameValidator.EnsureFeaturePath("../escape"));
    }

    [Fact]
    public void EnsureFeaturePath_SingleSegment_DoesNotThrow()
    {
        var ex = Record.Exception(() => CSharpNameValidator.EnsureFeaturePath("Test"));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureFeaturePath_NestedValid_DoesNotThrow()
    {
        var ex = Record.Exception(() => CSharpNameValidator.EnsureFeaturePath("Document/ReferenceDocuments"));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureFeaturePath_ThreeLevelsNested_DoesNotThrow()
    {
        var ex = Record.Exception(() => CSharpNameValidator.EnsureFeaturePath("A/B/C"));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureFeaturePath_SegmentWithKeyword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CSharpNameValidator.EnsureFeaturePath("Test/class"));
    }
}
