using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: searching, refusing and exporting live in AuditHistoryService, and the
/// page cannot edit or delete a line because it has no command that could.
/// </summary>
public partial class AuditView : UserControl
{
    public AuditView()
    {
        InitializeComponent();
    }
}
