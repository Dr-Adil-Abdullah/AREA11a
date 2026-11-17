using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class PurchasesView : UserControl
{
    private readonly DispatcherTimer _accessTimer;

    public PurchasesView()
    {
        InitializeComponent();
        _accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _accessTimer.Tick += CheckEntryAccess;
        Loaded += OnLoaded;
        Unloaded += (_, _) => _accessTimer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CheckEntryAccess(this, EventArgs.Empty);
        _accessTimer.Start();
    }

    private void CheckEntryAccess(object? sender, EventArgs e)
    {
        if (DataContext is PurchasesViewModel viewModel) viewModel.RefreshEntryAccess();
    }
}
