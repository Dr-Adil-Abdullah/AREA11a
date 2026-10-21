using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the two lists and the two commands live on the view-model, and the
/// rule (owner only, reason compulsory) is enforced by AlertService, not by this screen.
/// </summary>
public partial class AlertsView : UserControl
{
    public AlertsView()
    {
        InitializeComponent();
    }
}
