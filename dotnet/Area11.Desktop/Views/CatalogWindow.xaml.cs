using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>E-04 catalog window; all behavior is in the view-model and ProductService.</summary>
public partial class CatalogWindow : Window
{
    public CatalogWindow(CatalogWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
