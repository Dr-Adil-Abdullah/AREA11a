using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Loaded/unloaded lifecycle only; current-access cleanup belongs to the viewmodel and Core.
/// </summary>
public partial class CustomFieldsEditorView : UserControl
{
    public CustomFieldsEditorView()
    {
        InitializeComponent();
        var accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        accessTimer.Tick += (_, _) => (DataContext as CustomFieldsEditorViewModel)?.RefreshAccess();
        Loaded += (_, _) => { (DataContext as CustomFieldsEditorViewModel)?.RefreshAccess(); accessTimer.Start(); };
        Unloaded += (_, _) => accessTimer.Stop();
    }
}
