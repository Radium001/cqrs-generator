namespace CqrsGenerator.Gui.ViewModels;

public sealed class GenerationActionDescriptor
{
    public GenerationActionDescriptor(
        string actionId,
        string displayName,
        string groupName,
        string statusText,
        bool isImplemented)
    {
        ActionId = actionId;
        DisplayName = displayName;
        GroupName = groupName;
        StatusText = statusText;
        IsImplemented = isImplemented;
    }

    public string ActionId { get; }

    public string DisplayName { get; }

    public string GroupName { get; }

    public string StatusText { get; }

    public bool IsImplemented { get; }
}
