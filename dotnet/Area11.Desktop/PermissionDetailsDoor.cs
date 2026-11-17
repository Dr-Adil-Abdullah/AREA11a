using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

internal static class PermissionDetailsDoor
{
    public static void Show(AppServices services, User user, string module)
    {
        var window = new PermissionDetailsWindow(new PermissionDetailsViewModel(services, user, module));
        if (Application.Current?.MainWindow is Window owner && !ReferenceEquals(owner, window))
            window.Owner = owner;
        window.ShowDialog();
    }
}
