using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// The barcode window. Code-behind only names the window and sets the data context; the codes are
/// read and written by BarcodeService.
/// </summary>
public partial class BarcodeWindow : Window
{
    public BarcodeWindow(BarcodeListViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        Title = viewModel.Title;
    }
}
