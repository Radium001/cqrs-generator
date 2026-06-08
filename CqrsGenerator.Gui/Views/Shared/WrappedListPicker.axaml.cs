using Avalonia.Controls;
using Avalonia.Input;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Views.Shared;

public partial class WrappedListPicker : UserControl
{
    public WrappedListPicker()
    {
        InitializeComponent();
    }

    private void OnPickerKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not WrappedListPickerViewModel vm)
            return;

        switch (e.Key)
        {
            case Key.Down:
                if (!vm.IsSelectionLocked)
                    vm.SelectNext();
                e.Handled = true;
                break;
            case Key.Up:
                if (!vm.IsSelectionLocked)
                    vm.SelectPrevious();
                e.Handled = true;
                break;
            case Key.Left when CanCycleBackward(sender):
                vm.CyclePrefixBackwardCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right when CanCycleForward(sender):
                vm.CyclePrefixForwardCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private static bool CanCycleBackward(object? sender) =>
        sender switch
        {
            TextBox { CaretIndex: 0 } => true,
            ListBox => true,
            _ => false,
        };

    private static bool CanCycleForward(object? sender) =>
        sender switch
        {
            TextBox { Text: not null } tb when tb.CaretIndex == tb.Text.Length => true,
            ListBox => true,
            _ => false,
        };
}
