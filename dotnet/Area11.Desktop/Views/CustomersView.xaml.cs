using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind carries the list's ticked rows to the view-model; the list, the selected customer's
/// book and the receive box are bound to CustomersViewModel, and CustomerLedgerService owns every
/// rule (over-payment refused, the balance rebuilt from history, the mismatch warning). A list
/// cannot bind its own multi-selection, so the ticked set travels through
/// <c>SelectionChanged</c> (Portion 11 bulk printing).
/// </summary>
public partial class CustomersView : UserControl
{
    private readonly DispatcherTimer _accessTimer;

    public CustomersView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += CheckAccess;
        Loaded += (_, _) => { CheckAccess(this, EventArgs.Empty); _accessTimer.Start(); };
        Unloaded += (_, _) => _accessTimer.Stop();
    }

    private void CustomersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CustomersViewModel viewModel && sender is ListBox list)
        {
            viewModel.SetPrintSelection(list.SelectedItems.OfType<CustomerSummary>());
        }
    }
    private void CheckAccess(object? sender, EventArgs e)
    {
        if (DataContext is CustomersViewModel viewModel) viewModel.RefreshAccess();
    }

}
