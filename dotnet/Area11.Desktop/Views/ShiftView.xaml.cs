using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the float, the count and the verdict live on the view-model, and
/// ShiftService rebuilds the expected cash from history - the screen never does the arithmetic.
/// </summary>
public partial class ShiftView : UserControl
{
    public ShiftView()
    {
        InitializeComponent();
    }
}
