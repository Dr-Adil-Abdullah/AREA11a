using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind refreshes the handover roster only: the float, the count and the verdict live on the view-model, and
/// ShiftService rebuilds the expected cash from history - the screen never does the arithmetic.
/// </summary>
public partial class ShiftView : UserControl
{
    private readonly DispatcherTimer _accessTimer;

    public ShiftView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += CheckAccess;
        Loaded += (_, _) => { CheckAccess(this, EventArgs.Empty); _accessTimer.Start(); };
        Unloaded += (_, _) => _accessTimer.Stop();
    }
    private void CheckAccess(object? sender, EventArgs e)
    {
        if (DataContext is ShiftViewModel viewModel) viewModel.RefreshPeople();
    }

}
