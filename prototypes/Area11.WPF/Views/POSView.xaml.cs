using System.Windows.Controls;
using System.Windows.Input;
using Area11.WPF.ViewModels;

namespace Area11.WPF.Views;

public partial class POSView : UserControl
{
    public POSView()
    {
        InitializeComponent();
    }

    private void OnProductDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is POSViewModel viewModel && viewModel.AddToCartCommand.CanExecute(null))
        {
            viewModel.AddToCartCommand.Execute(null);
        }
    }
}
