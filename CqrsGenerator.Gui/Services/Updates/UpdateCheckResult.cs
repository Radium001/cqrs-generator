namespace CqrsGenerator.Gui.Services.Updates;

public sealed record UpdateCheckResult(
    bool IsSupported,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? AvailableVersion,
    string? Message,
    object? NativeUpdateInfo);
