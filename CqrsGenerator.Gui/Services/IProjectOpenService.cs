namespace CqrsGenerator.Gui.Services;

public interface IProjectOpenService
{
    Task<string?> OpenProjectAsync(CancellationToken cancellationToken);
}
