using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Tests;

public class GenerationPlanTests
{
    // ── AddCreateFile ──

    [Fact]
    public void AddCreateFile_FileExists_AddsConflict()
    {
        using var tmp = new TempProject();
        var filePath = tmp.AddFile("test.cs", "existing");
        var plan = new GenerationPlan();
        plan.AddCreateFile(filePath, "new content");

        Assert.Single(plan.Conflicts);
        Assert.Contains("уже существует", plan.Conflicts[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(plan.Operations);
    }

    [Fact]
    public void AddCreateFile_FileDoesNotExist_NoConflict()
    {
        using var tmp = new TempProject();
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "new.cs"), "content");

        Assert.Empty(plan.Conflicts);
        Assert.Single(plan.Operations);
        Assert.Equal(GenerationOperationKind.CreateFile, plan.Operations[0].Kind);
    }

    // ── AddUpdateFile ──

    [Fact]
    public void AddUpdateFile_FileDoesNotExist_AddsConflict()
    {
        using var tmp = new TempProject();
        var plan = new GenerationPlan();
        plan.AddUpdateFile(Path.Combine(tmp.Root, "missing.cs"), "content");

        Assert.Single(plan.Conflicts);
        Assert.Contains("не найден", plan.Conflicts[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(plan.Operations);
    }

    [Fact]
    public void AddUpdateFile_FileExists_NoConflict()
    {
        using var tmp = new TempProject();
        var filePath = tmp.AddFile("existing.cs", "old");
        var plan = new GenerationPlan();
        plan.AddUpdateFile(filePath, "new content");

        Assert.Empty(plan.Conflicts);
        Assert.Single(plan.Operations);
        Assert.Equal(GenerationOperationKind.UpdateFile, plan.Operations[0].Kind);
        var update = Assert.IsType<UpdateFileOperation>(plan.Operations[0]);
        Assert.Equal("old", update.OriginalContent);
    }

    // ── Merge ──

    [Fact]
    public void Merge_CombinesOperations()
    {
        using var tmp = new TempProject();
        var p1 = new GenerationPlan();
        p1.AddCreateFile(Path.Combine(tmp.Root, "a.cs"), "a");
        p1.AddWarning("w1");

        var p2 = new GenerationPlan();
        p2.AddCreateFile(Path.Combine(tmp.Root, "b.cs"), "b");
        p2.AddWarning("w2");

        p1.Merge(p2);
        Assert.Equal(2, p1.Operations.Count);
        Assert.Equal(2, p1.Warnings.Count);
    }

    [Fact]
    public void Merge_CombinesConflicts()
    {
        using var tmp = new TempProject();
        var p1 = new GenerationPlan();
        p1.AddConflict("a", "msg");

        var p2 = new GenerationPlan();
        p2.AddConflict("b", "msg2");

        p1.Merge(p2);
        Assert.Equal(2, p1.Conflicts.Count);
    }

    [Fact]
    public void Merge_UpdateFileWithTransform_ComposesTransforms()
    {
        using var tmp = new TempProject();
        var diPath = tmp.AddFile("Infrastructure/DependencyInjection.cs", """
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }
}
""");

        var p1 = new GenerationPlan();
        p1.TransformFile(diPath, content =>
            content.Replace("return services;", "    services.AddScoped<IA, A>();\n        return services;"));

        var p2 = new GenerationPlan();
        p2.TransformFile(diPath, content =>
            content.Replace("return services;", "    services.AddScoped<IB, B>();\n        return services;"));

        p1.Merge(p2);

        Assert.Empty(p1.Conflicts);
        Assert.Single(p1.Operations);
        var update = Assert.IsType<UpdateFileOperation>(p1.Operations[0]);
        Assert.Contains("services.AddScoped<IA, A>", update.Content);
        Assert.Contains("services.AddScoped<IB, B>", update.Content);
    }

    // ── Files ──

    [Fact]
    public void Files_OnlyReturnsCreateFileOperations()
    {
        using var tmp = new TempProject();
        var filePath = tmp.AddFile("existing.cs", "old");
        var plan = new GenerationPlan();
        plan.AddCreateFile(Path.Combine(tmp.Root, "new.cs"), "new");
        plan.AddUpdateFile(filePath, "updated");
        plan.AddDirectory(Path.Combine(tmp.Root, "dir"));

        Assert.Single(plan.Files);
        Assert.Contains("new.cs", plan.Files[0].Path);
    }

    // ── HasConflicts ──

    [Fact]
    public void HasConflicts_WithConflict_ReturnsTrue()
    {
        var plan = new GenerationPlan();
        plan.AddConflict("x", "y");
        Assert.True(plan.HasConflicts);
    }

    [Fact]
    public void HasConflicts_Empty_ReturnsFalse()
    {
        Assert.False(new GenerationPlan().HasConflicts);
    }

    // ── AddDirectory ──

    [Fact]
    public void AddDirectory_AddsCreateDirectoryOperation()
    {
        var plan = new GenerationPlan();
        plan.AddDirectory("C:\\some\\dir");

        Assert.Single(plan.Operations);
        Assert.Equal(GenerationOperationKind.CreateDirectory, plan.Operations[0].Kind);
    }

    [Fact]
    public void PlanPreparationService_Prepare_AddsExplicitParentDirectoriesForCreateFile()
    {
        using var tmp = new TempProject();
        var filePath = Path.Combine(tmp.Root, "Application", "Features", "Users", "Queries", "GetUsersQuery.cs");
        var plan = new GenerationPlan();
        plan.AddCreateFile(filePath, "content");

        var package = new PlanPreparationService().Prepare(plan, tmp.Root);

        Assert.Contains(package.Operations, operation => operation is PreparedCreateDirectoryOperation directory && directory.RelativePath == "Application");
        Assert.Contains(package.Operations, operation => operation is PreparedCreateFileOperation file && file.RelativePath == "Application/Features/Users/Queries/GetUsersQuery.cs");
    }

    [Fact]
    public void PlanPreparationService_Prepare_AddsConflictForPathOutsideTargetRoot()
    {
        using var tmp = new TempProject();
        var outsidePath = Path.Combine(Path.GetTempPath(), "other-root", "Foo.cs");
        var plan = new GenerationPlan();
        plan.AddCreateFile(outsidePath, "content");

        var package = new PlanPreparationService().Prepare(plan, tmp.Root);

        Assert.NotEmpty(package.Conflicts);
        Assert.DoesNotContain(package.Operations, operation => operation.Path == Path.GetFullPath(outsidePath));
    }

    [Fact]
    public void StrictPlanApplier_Apply_RejectsUpdatedFileDriftBeforeWriting()
    {
        using var tmp = new TempProject();
        var filePath = tmp.AddFile("existing.cs", "current");
        var package = new PreparedApplyPackage
        {
            TargetRootPath = tmp.Root,
            Fingerprint = "test",
            Warnings = [],
            Conflicts = [],
            Operations =
            [
                new PreparedUpdateFileOperation(
                    filePath,
                    "existing.cs",
                    "new content",
                    "old content",
                    "hash")
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new StrictPlanApplier().Apply(package));

        Assert.Contains("changed after the plan was built", ex.Message);
        Assert.Equal("current", File.ReadAllText(filePath));
    }

    [Fact]
    public void StrictPlanApplier_Apply_RollsBackCreatedAndUpdatedFilesOnFailure()
    {
        using var tmp = new TempProject();
        var updatedFile = tmp.AddFile("existing.cs", "old");
        var createdFile = Path.Combine(tmp.Root, "new.cs");
        var package = new PreparedApplyPackage
        {
            TargetRootPath = tmp.Root,
            Fingerprint = "test",
            Warnings = [],
            Conflicts = [],
            Operations =
            [
                new PreparedUpdateFileOperation(updatedFile, "existing.cs", "new", "old", "hash"),
                new PreparedCreateFileOperation(createdFile, "new.cs", "created"),
            ]
        };

        var fileSystem = new FailingPreparedApplyFileSystem(new LocalPreparedApplyFileSystem(), failOnWriteNumber: 2);
        var ex = Assert.Throws<InvalidOperationException>(() => new StrictPlanApplier(fileSystem).Apply(package));

        Assert.Contains("rolled back", ex.Message);
        Assert.Equal("old", File.ReadAllText(updatedFile));
        Assert.False(File.Exists(createdFile));
    }

    [Fact]
    public void StrictPlanApplier_Apply_RefusesImplicitDirectoryCreation()
    {
        using var tmp = new TempProject();
        var filePath = Path.Combine(tmp.Root, "nested", "file.cs");
        var package = new PreparedApplyPackage
        {
            TargetRootPath = tmp.Root,
            Fingerprint = "test",
            Warnings = [],
            Conflicts = [],
            Operations =
            [
                new PreparedCreateFileOperation(filePath, "nested/file.cs", "content"),
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new StrictPlanApplier().Apply(package));

        Assert.Contains("parent directory is not part of the prepared package", ex.Message);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void StrictPlanApplier_Apply_DeletesFileAndRollsBackOnFailure()
    {
        using var tmp = new TempProject();
        var deletedFile = tmp.AddFile("existing.cs", "old");
        var createdFile = Path.Combine(tmp.Root, "new.cs");
        var package = new PreparedApplyPackage
        {
            TargetRootPath = tmp.Root,
            Fingerprint = "test",
            Warnings = [],
            Conflicts = [],
            Operations =
            [
                new PreparedDeleteFileOperation(deletedFile, "existing.cs", "old", "hash"),
                new PreparedCreateFileOperation(createdFile, "new.cs", "created"),
            ]
        };

        var fileSystem = new FailingPreparedApplyFileSystem(new LocalPreparedApplyFileSystem(), failOnWriteNumber: 1);
        var ex = Assert.Throws<InvalidOperationException>(() => new StrictPlanApplier(fileSystem).Apply(package));

        Assert.Contains("rolled back", ex.Message);
        Assert.True(File.Exists(deletedFile));
        Assert.Equal("old", File.ReadAllText(deletedFile));
    }

    private sealed class FailingPreparedApplyFileSystem : IPreparedApplyFileSystem
    {
        private readonly IPreparedApplyFileSystem _inner;
        private readonly int _failOnWriteNumber;
        private int _writeCount;

        public FailingPreparedApplyFileSystem(IPreparedApplyFileSystem inner, int failOnWriteNumber)
        {
            _inner = inner;
            _failOnWriteNumber = failOnWriteNumber;
        }

        public bool FileExists(string path) => _inner.FileExists(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public void WriteAllText(string path, string content)
        {
            _writeCount++;
            if (_writeCount == _failOnWriteNumber)
            {
                throw new IOException("Injected write failure.");
            }

            _inner.WriteAllText(path, content);
        }

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);

        public void DeleteFile(string path) => _inner.DeleteFile(path);

        public void DeleteDirectory(string path) => _inner.DeleteDirectory(path);

        public bool IsDirectoryEmpty(string path) => _inner.IsDirectoryEmpty(path);
    }
}
