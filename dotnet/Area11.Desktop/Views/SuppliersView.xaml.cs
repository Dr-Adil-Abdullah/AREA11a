using System.Windows.Controls;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind carries the list's ticked rows to the view-model; the supplier list, the account and
/// the SR tray are bound to SuppliersViewModel, and SupplierLedgerService owns every rule
/// (over-payment refused, the payable rebuilt from history, new stock decrements refused below zero
/// under D-93). A list cannot bind its own multi-selection, so the ticked set travels through
/// <c>SelectionChanged</c> (Portion 11 bulk printing).
/// </summary>
public partial class SuppliersView : UserControl
{
    public SuppliersView()
    {
        InitializeComponent();
    }

    private void SuppliersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SuppliersViewModel viewModel && sender is ListBox list)
        {
            viewModel.SetPrintSelection(list.SelectedItems.OfType<SupplierSummary>());
        }
    }
}
