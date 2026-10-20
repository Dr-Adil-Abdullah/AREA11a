using System.Windows;
using Area11.WPF.ViewModels;

namespace Area11.WPF.Views;

public partial class ShellView : Window
{
    public ShellView(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
