using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class SaleSuccessWindow : Window
{
    public SaleSuccessWindow(SaleSuccessViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
