using CqrsGenerator.Core.Editing;

namespace CqrsGenerator.Tests;

public class EditingTests
{
    private static readonly CSharpSyntaxEditor Editor = new();

    private const string InterfaceSource = """
namespace TestNs;

public interface IFoo
{
}
""";

    private const string ClassSource = """
namespace TestNs;

public class Foo
{
}
""";

    private const string DiSource = """
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOldService, OldService>();
        return services;
    }
}
""";

    // ── AddMethodToInterface ──

    [Fact]
    public void AddMethodToInterface_Success_AddsMethod()
    {
        var result = Editor.AddMethodToInterface(InterfaceSource, "IFoo",
            "Task<int>", "GetData", [("string", "param")]);

        Assert.Contains("Task<int> GetData(string param);", result);
    }

    [Fact]
    public void AddMethodToInterface_InterfaceNotFound_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Editor.AddMethodToInterface(InterfaceSource, "INonexistent",
                "void", "M", []));
    }

    [Fact]
    public void AddMethodToInterface_MethodAlreadyExists_Throws()
    {
        var oneMethod = """
namespace TestNs;
public interface IFoo
{
    Task<int> GetData();
}
""";
        Assert.Throws<InvalidOperationException>(() =>
            Editor.AddMethodToInterface(oneMethod, "IFoo",
                "void", "GetData", []));
    }

    [Fact]
    public void AddMethodToInterface_WithDefaultParameter_AddsDefault()
    {
        var result = Editor.AddMethodToInterface(InterfaceSource, "IFoo",
            "Task<int>", "Handle", [("CancellationToken", "ct")],
            new HashSet<string> { "ct" });

        Assert.Contains("CancellationToken ct = default", result);
    }

    [Fact]
    public void AddMethodToInterface_PreservesClosingBraceIndent()
    {
        var source = """
namespace TestNs
{
    public interface IFoo
    {
        Task<int> ExistingAsync();
    }
}
""";

        var result = Editor.AddMethodToInterface(source, "IFoo",
            "Task<int>", "GetDataAsync", [("string", "param")]);

        var expected = """
namespace TestNs
{
    public interface IFoo
    {
        Task<int> ExistingAsync();
        Task<int> GetDataAsync(string param);
    }
}
""";

        Assert.Equal(expected, result);
    }

    // ── AddMethodToClass ──

    [Fact]
    public void AddMethodToClass_Success_AddsAsyncMethod()
    {
        var result = Editor.AddMethodToClass(ClassSource, "Foo",
            "Task<string>", "Execute", [("int", "id")],
            "throw new NotImplementedException();");

        Assert.Contains("public async Task<string> Execute(int id)", result);
        Assert.Contains("throw new NotImplementedException();", result);
    }

    [Fact]
    public void AddMethodToClass_ClassNotFound_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Editor.AddMethodToClass(ClassSource, "NoSuchClass",
                "void", "M", [], "return;"));
    }

    [Fact]
    public void AddMethodToClass_MethodAlreadyExists_Throws()
    {
        var hasMethod = """
namespace TestNs;
public class Foo
{
    public async Task<int> Execute()
    {
        return 42;
    }
}
""";
        Assert.Throws<InvalidOperationException>(() =>
            Editor.AddMethodToClass(hasMethod, "Foo",
                "void", "Execute", [], "return;"));
    }

    [Fact]
    public void AddMethodToClass_WithDefaultParameter_AddsDefault()
    {
        var result = Editor.AddMethodToClass(ClassSource, "Foo",
            "Task<int>", "Handle", [("CancellationToken", "ct")],
            "return null!;",
            new HashSet<string> { "ct" });

        Assert.Contains("CancellationToken ct = default", result);
    }

    [Fact]
    public void AddMethodToClass_PreservesClosingBraceIndent()
    {
        var source = """
namespace TestNs
{
    public class Foo
    {
        public Task<int> ExistingAsync()
        {
            return Task.FromResult(42);
        }
    }
}
""";

        var result = Editor.AddMethodToClass(source, "Foo",
            "Task<string>", "ExecuteAsync", [("int", "id")],
            "return id.ToString();");

        var expected = """
namespace TestNs
{
    public class Foo
    {
        public Task<int> ExistingAsync()
        {
            return Task.FromResult(42);
        }
        public async Task<string> ExecuteAsync(int id)
        {
            return id.ToString();
        }
    }
}
""";

        Assert.Equal(expected, result);
    }

    // ── AddScopedRegistration ──

    [Fact]
    public void AddScopedRegistration_RegistrationExists_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Editor.AddScopedRegistration(DiSource, "IOldService", "OldService"));
    }

    [Fact]
    public void AddScopedRegistration_MethodNotFound_Throws()
    {
        var noMethod = "public static class X { }";
        Assert.Throws<InvalidOperationException>(() =>
            Editor.AddScopedRegistration(noMethod, "INew", "New"));
    }

    [Fact]
    public void AddScopedRegistration_Success_BeforeReturn_InsertsBeforeReturn()
    {
        var result = Editor.AddScopedRegistration(DiSource, "INewService", "NewService");

        Assert.Contains("services.AddScoped<INewService, NewService>();", result);
        var idxReg = result.IndexOf("AddScoped<INewService", StringComparison.Ordinal);
        var idxReturn = result.IndexOf("return services;", StringComparison.Ordinal);
        Assert.True(idxReg < idxReturn);
    }

    [Fact]
    public void AddScopedRegistration_PreservesExistingBlankLines()
    {
        var source = """
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOldService, OldService>();

        return services;
    }
}
""";

        var result = Editor.AddScopedRegistration(source, "INewService", "NewService");

        Assert.Contains("services.AddScoped<IOldService, OldService>();\n        services.AddScoped<INewService, NewService>();\n\n        return services;", result);
    }

    [Fact]
    public void AddScopedRegistration_Success_WithoutReturn_AppendsAtEnd()
    {
        var noReturn = """
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOldService, OldService>();
    }
}
""";
        var result = Editor.AddScopedRegistration(noReturn, "INewService", "NewService");
        Assert.Contains("services.AddScoped<INewService, NewService>();", result);
    }

    [Fact]
    public void AddScopedRegistration_DifferentMethodName_AddsRegistration()
    {
        // ContainsRegistration only checks AddScoped calls,
        // so a different method name won't be detected as duplicate
        var withOther = """
public static class DependencyInjection
{
    public static void Register(IServiceCollection services)
    {
        services.AddTransient<IFoo, Foo>();
    }
}
""";
        var result = Editor.AddScopedRegistration(withOther, "IFoo", "Foo", "Register");
        Assert.Contains("services.AddScoped<IFoo, Foo>();", result);
    }

    // ── ParseRoot ──

    [Fact]
    public void ParseRoot_InvalidCSharp_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Editor.AddMethodToInterface("}{ broken syntax [", "IFoo", "void", "M", []));
    }

    [Fact]
    public void ParseRoot_ValidCSharp_Succeeds()
    {
        var ex = Record.Exception(() =>
            Editor.AddMethodToInterface(InterfaceSource, "IFoo", "void", "NewMethod", []));
        Assert.Null(ex);
    }

    [Fact]
    public void ReplaceNamespace_BlockScopedNamespace_NormalizesBracePlacement()
    {
        var source = """
namespace Old.Namespace{
    public class UserDto
    {
        public int Id { get; set; }
    }
}
""";

        var result = Editor.ReplaceNamespace(source, "Application.Features.Users.DTOs");

        var expected = """
namespace Application.Features.Users.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
    }
}
""";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceNamespace_FileScopedNamespace_PreservesFileScopedForm()
    {
        var source = """
namespace Old.Namespace;

public class UserDto
{
    public int Id { get; set; }
}
""";

        var result = Editor.ReplaceNamespace(source, "Application.Features.Users.DTOs");

        Assert.Equal("""
namespace Application.Features.Users.DTOs;

public class UserDto
{
    public int Id { get; set; }
}
""", result);
    }
}
