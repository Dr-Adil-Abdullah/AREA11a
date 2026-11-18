using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind refreshes current parent-tab access only. The three tabs are the existing customer, recovery and supplier views,
/// fed by PartnersViewModel; every rule still lives in the services they call.
/// </summary>
public partial class PartnersView : UserControl
{
    private readonly DispatcherTimer _accessTimer;

    public PartnersView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += CheckAccess;
        Loaded += (_, _) => { CheckAccess(this, EventArgs.Empty); _accessTimer.Start(); };
        Unloaded += (_, _) => _accessTimer.Stop();
    }
    private void CheckAccess(object? sender, EventArgs e)
    {
        if (DataContext is PartnersViewModel viewModel) viewModel.RefreshAccess();
    }

}
