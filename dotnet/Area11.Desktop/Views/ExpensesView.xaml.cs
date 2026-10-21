using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the two forms and the list live on the view-model, and ExpenseService
/// owns every rule (permission, cost valuation, the drawer warning).
/// </summary>
public partial class ExpensesView : UserControl
{
    public ExpensesView()
    {
        InitializeComponent();
    }
}
