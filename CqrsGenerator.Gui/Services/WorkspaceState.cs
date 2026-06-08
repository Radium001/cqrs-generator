using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Discovery;
using CqrsGenerator.Core.Generation;
using CqrsGenerator.Core.Validation;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed record WorkspaceState(
    string? TargetRootPath,
    GeneratorConfig? Config,
    ProjectModel? ProjectModel,
    GenerationPlan? CurrentPlan,
    PreparedApplyPackage? CurrentPreparedApplyPackage,
    PlanPreviewSnapshot CurrentPlanPreviewSnapshot,
    IReadOnlyList<GenerationWarning> GenerationWarnings,
    PlanBuildStatus PlanBuildStatus,
    string? PlanBuildErrorMessage,
    string? LastErrorDetails,
    string StatusText,
    bool CanBuildPlan,
    bool CanApplyPlan,
    bool IsProjectLoaded,
    bool IsScanning,
    string? ApplyResultMessage,
    IReadOnlyList<ArchitectureWarning> Warnings)
{
    public static WorkspaceState Empty { get; } = new(
        null,
        null,
        null,
        null,
        null,
        PlanPreviewSnapshot.Empty("Build plan to see affected files."),
        [],
        PlanBuildStatus.Idle,
        null,
        null,
        "Open a project.",
        false,
        false,
        false,
        false,
        null,
        []);

    public ProjectWorkspaceContext? ProjectContext =>
        Config is not null && ProjectModel is not null && !string.IsNullOrWhiteSpace(TargetRootPath)
            ? new ProjectWorkspaceContext(TargetRootPath, Config, ProjectModel)
            : null;
}
