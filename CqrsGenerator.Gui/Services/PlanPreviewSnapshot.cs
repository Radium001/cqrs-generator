using CqrsGenerator.Gui.ViewModels;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Services;

public sealed class PlanPreviewSnapshot
{
    public static PlanPreviewSnapshot Empty(string emptyStateText) =>
        new()
        {
            SummaryItems =
            [
                new PreviewSummaryItemViewModel("Create", "0"),
                new PreviewSummaryItemViewModel("Update", "0"),
                new PreviewSummaryItemViewModel("Warnings", "0"),
                new PreviewSummaryItemViewModel("Conflicts", "0"),
            ],
            RootNodes = [],
            DefaultPreview = new FilePreviewViewModel(FilePreviewKind.None, null, emptyStateText),
            EmptyStateText = emptyStateText,
            GenerationWarnings = [],
        };

    public static PlanPreviewSnapshot Error(string errorText) =>
        new()
        {
            SummaryItems =
            [
                new PreviewSummaryItemViewModel("Create", "0"),
                new PreviewSummaryItemViewModel("Update", "0"),
                new PreviewSummaryItemViewModel("Warnings", "0"),
                new PreviewSummaryItemViewModel("Conflicts", "0"),
            ],
            RootNodes = [],
            DefaultPreview = new FilePreviewViewModel(FilePreviewKind.None, null, errorText),
            EmptyStateText = errorText,
            IsError = true,
            GenerationWarnings = [],
        };

    public required IReadOnlyList<PreviewSummaryItemViewModel> SummaryItems { get; init; }

    public required IReadOnlyList<PlanTreeNodeViewModel> RootNodes { get; init; }

    public required FilePreviewViewModel DefaultPreview { get; init; }

    public required string EmptyStateText { get; init; }

    public required IReadOnlyList<GenerationWarning> GenerationWarnings { get; init; }

    public bool IsError { get; init; }

    public string? PackageFingerprint { get; init; }
}
