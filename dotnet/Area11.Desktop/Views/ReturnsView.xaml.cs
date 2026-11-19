using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>Only access refresh lifecycle; return accounting remains in Core.</summary>
public partial class ReturnsView : UserControl
{
    private readonly DispatcherTimer _accessTimer;
    public ReturnsView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += (_, _) => CheckAccess();
        Loaded += (_, _) => { CheckAccess(); _accessTimer.Start(); };
        Unloaded += (_, _) => _accessTimer.Stop();
    }
    private void CheckAccess()
    {
        if (DataContext is ReturnsViewModel model) model.RefreshPostingAccess();
    }
}
