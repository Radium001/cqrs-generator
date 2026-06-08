using Avalonia.Controls;
using Avalonia.Input;
using CqrsGenerator.Gui.ViewModels;

namespace CqrsGenerator.Gui.Views.Shared;

public partial class MultiSelectListPicker : UserControl
{
    public MultiSelectListPicker()
    {
        InitializeComponent();
    }

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MultiSelectListPickerViewModel vm)
            return;

        switch (e.Key)
        {
            case Key.Down:
                vm.SelectNext();
                e.Handled = true;
                break;
            case Key.Up:
                vm.SelectPrevious();
                e.Handled = true;
                break;
        }
    }
}
