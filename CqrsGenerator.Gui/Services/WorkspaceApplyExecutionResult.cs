namespace CqrsGenerator.Gui.Services;

public sealed record WorkspaceApplyExecutionResult(
    bool Succeeded,
    bool Cancelled,
    string StatusText,
    string? ErrorMessage = null)
{
    public static WorkspaceApplyExecutionResult Success(string statusText) => new(true, false, statusText);

    public static WorkspaceApplyExecutionResult CancelledByUser(string statusText) => new(false, true, statusText);

    public static WorkspaceApplyExecutionResult Failure(string statusText, string errorMessage) => new(false, false, statusText, errorMessage);

    public static WorkspaceApplyExecutionResult StalePackage(string statusText, string errorMessage) => new(false, false, statusText, errorMessage);
}
