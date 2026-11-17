using System.Windows;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class PermissionDetailsWindow : Window
{
    private readonly DispatcherTimer _refreshTimer;
    private readonly PermissionDetailsViewModel _viewModel;

    public PermissionDetailsWindow(PermissionDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += RefreshAssignments;
        Loaded += (_, _) => _refreshTimer.Start();
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Tick -= RefreshAssignments;
        };
    }

    private void RefreshAssignments(object? sender, EventArgs e) => _viewModel.Refresh();
}
