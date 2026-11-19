using System.Windows;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>Portion 5 drafts drawer; all behavior is in the view-model and DraftService.</summary>
public partial class DraftsView : Window
{
    private readonly DispatcherTimer _accessTimer;
    public DraftsView(DraftsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += (_, _) => viewModel.RefreshReadAccess();
        Loaded += (_, _) => { viewModel.RefreshReadAccess(); _accessTimer.Start(); };
        Closed += (_, _) => _accessTimer.Stop();
    }
}
