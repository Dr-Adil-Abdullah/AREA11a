using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>The one UI door to a medicine's history; view-models do not construct Windows.</summary>
internal static class ProductHistoryDoor
{
    public static void Show(AppServices services, User user, int productId, string productName)
    {
        var viewModel = new ProductHistoryViewModel(services, user, productId, productName);
        var window = new ProductHistoryWindow(viewModel);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive)
                    ?? Application.Current?.MainWindow;

        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
