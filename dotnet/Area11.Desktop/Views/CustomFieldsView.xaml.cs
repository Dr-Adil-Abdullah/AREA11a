using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Loaded/unloaded access-check lifecycle only. Core remains authoritative; unchanged ticks keep
/// the viewmodel's rows/selection/unsaved text stable.
/// </summary>
public partial class CustomFieldsView : UserControl
{
    public CustomFieldsView()
    {
        InitializeComponent();
        var accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        accessTimer.Tick += (_, _) => (DataContext as CustomFieldsViewModel)?.RefreshAccess();
        Loaded += (_, _) => { (DataContext as CustomFieldsViewModel)?.RefreshAccess(); accessTimer.Start(); };
        Unloaded += (_, _) => accessTimer.Stop();
    }
}
