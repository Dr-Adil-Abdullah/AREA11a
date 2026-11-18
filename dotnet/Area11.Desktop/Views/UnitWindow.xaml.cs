using System.Windows;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class UnitWindow : Window
{
    public UnitWindow(UnitWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        var accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        accessTimer.Tick += (_, _) => viewModel.RefreshAccess();
        Loaded += (_, _) => { viewModel.RefreshAccess(); accessTimer.Start(); };
        Closed += (_, _) => accessTimer.Stop();
    }
}
