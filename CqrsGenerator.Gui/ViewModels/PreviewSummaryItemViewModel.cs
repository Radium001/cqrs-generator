namespace CqrsGenerator.Gui.ViewModels;

public sealed class PreviewSummaryItemViewModel
{
    public PreviewSummaryItemViewModel(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }
}
