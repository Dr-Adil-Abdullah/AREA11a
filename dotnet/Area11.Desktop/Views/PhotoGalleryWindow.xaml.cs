using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// The photo window. Code-behind only sets the owner so the dialog cannot hide behind the main
/// window; the gallery itself is built by PhotoGalleryViewModel and PhotoService.
/// </summary>
public partial class PhotoGalleryWindow : Window
{
    public PhotoGalleryWindow(PhotoGalleryViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        Title = "Photos - " + viewModel.Title;
    }
}
