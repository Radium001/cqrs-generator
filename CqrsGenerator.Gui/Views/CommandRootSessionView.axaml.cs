using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CqrsGenerator.Gui.Views;

public sealed partial class CommandRootSessionView : UserControl
{
    public CommandRootSessionView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
