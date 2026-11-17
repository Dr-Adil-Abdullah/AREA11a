using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class ImportExportView : UserControl
{
    private readonly DispatcherTimer _accessTimer;

    public ImportExportView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += CheckAccess;
        Loaded += OnLoaded;
        Unloaded += (_, _) => _accessTimer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CheckAccess(this, EventArgs.Empty);
        _accessTimer.Start();
    }

    private void CheckAccess(object? sender, EventArgs e)
    {
        if (DataContext is ImportExportViewModel viewModel) viewModel.RefreshAccess();
    }
}
