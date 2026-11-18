using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>Active-view access refresh only; every number comes from <c>TaxViewModel</c>.</summary>
public partial class TaxView : UserControl
{
    private readonly DispatcherTimer _accessTimer;

    public TaxView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += CheckAccess;
        Loaded += (_, _) => { CheckAccess(this, EventArgs.Empty); _accessTimer.Start(); };
        Unloaded += (_, _) => _accessTimer.Stop();
    }
    private void CheckAccess(object? sender, EventArgs e)
    {
        if (DataContext is TaxViewModel viewModel) viewModel.RefreshAccess();
    }

}
