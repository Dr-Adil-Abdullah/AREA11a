using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the list and the message are built by ReorderService, and the page only
/// collects ticks and text (no business logic in a view).
/// </summary>
public partial class ReorderView : UserControl
{
    public ReorderView()
    {
        InitializeComponent();
    }
}
