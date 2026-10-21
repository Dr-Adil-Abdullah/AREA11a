using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the preview numbers, the history and the permission check are all on
/// the view-model, and StockService owns the rules (U-30 included).
/// </summary>
public partial class WriteOffView : UserControl
{
    public WriteOffView()
    {
        InitializeComponent();
    }
}
