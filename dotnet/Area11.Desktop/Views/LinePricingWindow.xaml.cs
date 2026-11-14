using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class LinePricingWindow : Window
{
    public LinePricingWindow(LinePricingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested = accepted => { DialogResult = accepted; };
        Closed += (_, _) => viewModel.CloseRequested = null;
    }
}
