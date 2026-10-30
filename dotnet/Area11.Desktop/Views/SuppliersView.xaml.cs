using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty. The supplier list, the account and the SR tray are bound to
/// SuppliersViewModel, and SupplierLedgerService owns every rule (over-payment refused, the payable
/// rebuilt from history, new stock decrements refused below zero under D-93).
/// </summary>
public partial class SuppliersView : UserControl
{
    public SuppliersView()
    {
        InitializeComponent();
    }
}
