using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class UnitWindow : Window
{
    public UnitWindow(UnitWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
