using System.Windows.Controls;
using System.Windows.Input;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
    }

    private void OnCounterPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Preview catches F4 before a focused ComboBox uses it to open its dropdown.
        // This is only a shortcut to the existing guarded command, never another save path.
        if (e.Key != Key.F4 || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }
        e.Handled = true;
        if (e.IsRepeat || DataContext is not PosViewModel viewModel)
        {
            return;
        }
        if (!CartGrid.CommitEdit(DataGridEditingUnit.Cell, true)
            || !CartGrid.CommitEdit(DataGridEditingUnit.Row, true))
        {
            return;
        }
        if (viewModel.CompleteCommand.CanExecute(null))
        {
            viewModel.CompleteCommand.Execute(null);
        }
    }
}
