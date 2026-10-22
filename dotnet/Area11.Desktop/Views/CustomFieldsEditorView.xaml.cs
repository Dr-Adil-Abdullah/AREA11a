using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty: the panel is built from the owner's fields by CustomFieldsEditorViewModel
/// and saved by CustomFieldService (no business logic in a view).
/// </summary>
public partial class CustomFieldsEditorView : UserControl
{
    public CustomFieldsEditorView()
    {
        InitializeComponent();
    }
}
