using System.Windows;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Window lifecycle only: current-access refresh while open, stop the timer on close.
/// Gallery state belongs to the viewmodel; authoritative authorization stays in Core.
/// </summary>
public partial class PhotoGalleryWindow : Window
{
    public PhotoGalleryWindow(PhotoGalleryViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        var accessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        accessTimer.Tick += (_, _) => viewModel.RefreshAccess();
        Loaded += (_, _) => { viewModel.RefreshAccess(); accessTimer.Start(); };
        Closed += (_, _) => accessTimer.Stop();
    }
}
