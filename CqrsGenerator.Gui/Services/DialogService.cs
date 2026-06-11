using Avalonia.Controls;
using Avalonia.Layout;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.Services;

public sealed class DialogService : IDialogService
{
    private readonly Window _ownerWindow;

    public DialogService(Window ownerWindow)
    {
        _ownerWindow = ownerWindow;
    }

    public async Task<bool> ConfirmPlanApplyAsync(PreparedApplyPackage package, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new Window
        {
            Title = "Apply plan",
            Width = 440,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog.Content = BuildContent(package, dialog);

        var result = await dialog.ShowDialog<bool>(_ownerWindow);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }


    public async Task<bool> ConfirmUpdateInstallAsync(string? availableVersion, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new Window
        {
            Title = "Install update",
            Width = 420,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog.Content = BuildUpdateContent(availableVersion, message, dialog);

        var result = await dialog.ShowDialog<bool>(_ownerWindow);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static Control BuildContent(PreparedApplyPackage package, Window dialog)
    {
        var operationsText = $"{package.Operations.Count} operation(s)";

        var applyButton = new Button
        {
            Content = "Apply",
            MinWidth = 90,
            IsDefault = true,
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 90,
            IsCancel = true,
        };

        var root = new Grid
        {
            Margin = new Avalonia.Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            RowSpacing = 16,
        };

        root.Children.Add(new TextBlock
        {
            Text = "Apply the current generation plan?",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var detailsPanel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = operationsText },
                new TextBlock
                {
                    Text = package.Warnings.Count == 0
                        ? "No generation warnings."
                        : $"{package.Warnings.Count} generation warning(s):",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = "Files will be written immediately after confirmation.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
            },
        };

        if (package.Warnings.Count > 0)
        {
            foreach (var warning in package.Warnings)
            {
                detailsPanel.Children.Add(new TextBlock
                {
                    Text = $"- {warning.Message}",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                });
            }
        }
        Grid.SetRow(detailsPanel, 1);
        root.Children.Add(detailsPanel);

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, applyButton },
        };
        Grid.SetRow(actionsPanel, 2);
        root.Children.Add(actionsPanel);

        applyButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        return root;
    }

    private static Control BuildUpdateContent(string? availableVersion, string message, Window dialog)
    {
        var installButton = new Button
        {
            Content = "Download and restart",
            MinWidth = 140,
            IsDefault = true,
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 90,
            IsCancel = true,
        };

        var root = new Grid
        {
            Margin = new Avalonia.Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            RowSpacing = 16,
        };

        root.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(availableVersion)
                ? "Install available update?"
                : $"Install version {availableVersion}?",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var detailsPanel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = "Save anything important before continuing.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
            },
        };

        Grid.SetRow(detailsPanel, 1);
        root.Children.Add(detailsPanel);

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, installButton },
        };
        Grid.SetRow(actionsPanel, 2);
        root.Children.Add(actionsPanel);

        installButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        return root;
    }

}
