using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>
/// The one door to the barcode window (F-43). A view-model must not build a Window itself, so the
/// Products page calls this instead - the same pattern the photo gallery uses.
/// </summary>
internal static class BarcodeWindowDoor
{
    public static void Show(AppServices services, User user, int productId, string productName)
    {
        if (productId <= 0)
        {
            return;
        }

        var viewModel = new BarcodeListViewModel(services, user, productId, productName);
        var window = new BarcodeWindow(viewModel);

        var owner = Application.Current?.MainWindow;

        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
