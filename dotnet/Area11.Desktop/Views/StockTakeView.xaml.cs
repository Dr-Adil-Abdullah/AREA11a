using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the frozen sheet, the count box and the two decisions are on the
/// view-model, and StockTakeService owns every rule (frozen cost, apply once, owner only).
/// </summary>
public partial class StockTakeView : UserControl
{
    public StockTakeView()
    {
        InitializeComponent();
    }
}
