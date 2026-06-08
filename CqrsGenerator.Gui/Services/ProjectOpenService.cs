using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CqrsGenerator.Gui.Services;

public sealed class ProjectOpenService(Window mainWindow) : IProjectOpenService
{
    public async Task<string?> OpenProjectAsync(CancellationToken cancellationToken)
    {
        var storage = mainWindow.StorageProvider;
        if (!storage.CanPickFolder)
        {
            throw new InvalidOperationException("Folder selection is not available on this platform.");
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select target project",
            AllowMultiple = false,
        });

        cancellationToken.ThrowIfCancellationRequested();

        if (folders.Count == 0)
        {
            return null;
        }

        var localPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new InvalidOperationException("The selected folder does not expose a local file-system path.");
        }

        return localPath;
    }
}
