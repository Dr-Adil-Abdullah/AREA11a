using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind refreshes active-view access only: the ageing, the payment and the slip all live in RecoveryService and
/// CustomerLedgerService, and the view-model only shapes the screen.
/// </summary>
public partial class RecoveryView : UserControl
{
    private readonly DispatcherTimer _accessTimer;

    public RecoveryView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += CheckAccess;
        Loaded += (_, _) => { CheckAccess(this, EventArgs.Empty); _accessTimer.Start(); };
        Unloaded += (_, _) => _accessTimer.Stop();
    }
    private void CheckAccess(object? sender, EventArgs e)
    {
        if (DataContext is RecoveryViewModel viewModel) viewModel.RefreshAccess();
    }

}
