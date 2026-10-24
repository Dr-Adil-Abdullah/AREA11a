using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty. The list, the selected customer's book and the receive box are bound to
/// CustomersViewModel, and CustomerLedgerService owns every rule (over-payment refused, the balance
/// rebuilt from history, the mismatch warning).
/// </summary>
public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();
    }
}
