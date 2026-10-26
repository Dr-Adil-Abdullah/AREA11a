using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>The one UI door to E-04 management; view-models do not construct Windows.</summary>
internal static class CatalogWindowDoor
{
    public static void Show(AppServices services, User user)
    {
        var viewModel = new CatalogWindowViewModel(services, user);
        var window = new CatalogWindow(viewModel);
        var owner = Application.Current?.MainWindow;

        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
