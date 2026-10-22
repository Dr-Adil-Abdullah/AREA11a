using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the fields are read and written by CustomFieldService, and the screen
/// only moves text between the boxes and that service (no business logic in a view).
/// </summary>
public partial class CustomFieldsView : UserControl
{
    public CustomFieldsView()
    {
        InitializeComponent();
    }
}
