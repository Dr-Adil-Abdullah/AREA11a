using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class NativeFieldsView : UserControl
{
    public NativeFieldsView()
    {
        InitializeComponent();
        var accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        accessTimer.Tick += (_, _) => (DataContext as NativeFieldsViewModel)?.RefreshAccess();
        Loaded += (_, _) => { (DataContext as NativeFieldsViewModel)?.Refresh(); accessTimer.Start(); };
        Unloaded += (_, _) => accessTimer.Stop();
    }
}
