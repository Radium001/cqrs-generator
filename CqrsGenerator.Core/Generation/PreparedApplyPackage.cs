namespace CqrsGenerator.Core.Generation;

public sealed class PreparedApplyPackage
{
    public required string TargetRootPath { get; init; }

    public required string Fingerprint { get; init; }

    public required IReadOnlyList<PreparedApplyOperation> Operations { get; init; }

    public required IReadOnlyList<GenerationWarning> Warnings { get; init; }

    public required IReadOnlyList<GenerationConflict> Conflicts { get; init; }

    public bool HasConflicts => Conflicts.Count > 0;
}
