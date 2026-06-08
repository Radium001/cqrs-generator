using Avalonia.Controls;
using Avalonia.Input;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Views.Shared;

public partial class CyclicInput : UserControl
{
    public CyclicInput()
    {
        InitializeComponent();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CyclicInputViewModel vm)
            return;

        switch (e.Key)
        {
            case Key.Up:
                vm.CycleForward();
                e.Handled = true;
                break;
            case Key.Down:
                vm.CycleBackward();
                e.Handled = true;
                break;
        }
    }
}
