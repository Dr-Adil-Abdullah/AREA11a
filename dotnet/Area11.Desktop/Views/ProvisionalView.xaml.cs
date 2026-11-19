using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>Current-access lifecycle only; financial policy stays in Core.</summary>
public partial class ProvisionalView : UserControl
{
    private readonly DispatcherTimer _accessTimer;
    public ProvisionalView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += (_, _) => CheckAccess();
        Loaded += (_, _) => { CheckAccess(); _accessTimer.Start(); };
        Unloaded += (_, _) => _accessTimer.Stop();
    }
    private void CheckAccess()
    {
        if (DataContext is ProvisionalViewModel model) model.RefreshAccess();
    }
}
