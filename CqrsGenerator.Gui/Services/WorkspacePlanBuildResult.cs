using CqrsGenerator.Core.Generation;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Services;

public sealed record WorkspacePlanBuildResult(
    bool Succeeded,
    string StatusText,
    PlanBuildStatus PlanBuildStatus,
    GenerationPlan? Plan,
    PreparedApplyPackage? PreparedPackage,
    PlanPreviewSnapshot Snapshot,
    string? ErrorMessage = null,
    string? ErrorDetails = null)
{
    public static WorkspacePlanBuildResult Success(string statusText, GenerationPlan plan, PreparedApplyPackage preparedPackage, PlanPreviewSnapshot snapshot) =>
        new(true, statusText, PlanBuildStatus.Built, plan, preparedPackage, snapshot);

    public static WorkspacePlanBuildResult Failure(
        string statusText,
        string errorMessage,
        string? errorDetails,
        PlanPreviewSnapshot snapshot) =>
        new(false, statusText, PlanBuildStatus.Failed, null, null, snapshot, errorMessage, errorDetails);
}
