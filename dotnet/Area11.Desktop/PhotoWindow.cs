using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>
/// The one door to the photo window (F-41). A view-model must not build a Window itself - that would
/// put the screen back inside the logic - so the three pages that own photos call this instead.
///
/// The window is modal and owned by the main window: it cannot slip behind the shop window, and the
/// page underneath is not reloaded while a photo is being added.
/// </summary>
internal static class PhotoWindow
{
    public static void Show(
        AppServices services,
        User user,
        string entity,
        int entityId,
        string title)
    {
        if (entityId <= 0)
        {
            return;
        }

        var viewModel = new PhotoGalleryViewModel(services, user, PhotoEntity.Clean(entity), entityId, title);
        var window = new PhotoGalleryWindow(viewModel);

        var owner = Application.Current?.MainWindow;

        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
