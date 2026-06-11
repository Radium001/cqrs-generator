namespace CqrsGenerator.Gui.Services.Updates;

public enum UpdateStatusKind
{
    Unknown,
    UnsupportedEnvironment,
    Checking,
    Latest,
    UpdateAvailable,
    Downloading,
    ReadyToRestart,
    Failed
}
