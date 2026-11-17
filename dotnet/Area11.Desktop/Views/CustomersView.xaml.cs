using System.Windows.Controls;
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
    public CustomersView()
    {
        InitializeComponent();
    }

    private void CustomersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CustomersViewModel viewModel && sender is ListBox list)
        {
            viewModel.SetPrintSelection(list.SelectedItems.OfType<CustomerSummary>());
        }
    }
}
