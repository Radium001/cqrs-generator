namespace CqrsGenerator.Gui.ViewModels.Generators;

public interface IGeneratorSessionViewModel
{
    string SessionId { get; }

    string DisplayName { get; }

    string Summary { get; }

    bool IsRoot { get; }

    bool HasUnsavedChanges { get; }

    bool CanClose { get; }
}
