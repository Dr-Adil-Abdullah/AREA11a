using System.Windows.Controls;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind names the view and carries the grid's ticked rows to the view-model; every number
/// comes from <c>SaleHistoryViewModel</c>. A grid cannot bind its own multi-selection, so the ticked
/// set travels through <c>SelectionChanged</c> (Portion 11 bulk printing).
/// </summary>
public partial class SaleHistoryView : UserControl
{
    public SaleHistoryView()
    {
        InitializeComponent();
    }

    private void BillsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SaleHistoryViewModel viewModel && sender is DataGrid grid)
        {
            viewModel.SetPrintSelection(grid.SelectedItems.OfType<SaleHistoryRow>());
        }
    }
}
