using System.Windows;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>E-04 catalog window; all behavior is in the view-model and ProductService.</summary>
public partial class CatalogWindow : Window
{
    public CatalogWindow(CatalogWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        var accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        accessTimer.Tick += (_, _) => viewModel.RefreshAccess();
        Loaded += (_, _) => { viewModel.RefreshAccess(); accessTimer.Start(); };
        Closed += (_, _) => accessTimer.Stop();
    }
}
