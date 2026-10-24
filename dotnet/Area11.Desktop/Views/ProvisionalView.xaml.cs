using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty. The two lists, the smart bill lookup and every rule live on the
/// view-model and in ProvisionalReturnService; this file only loads the XAML.
/// </summary>
public partial class ProvisionalView : UserControl
{
    public ProvisionalView()
    {
        InitializeComponent();
    }
}
