using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

internal static class UnitWindowDoor
{
    public static void Show(AppServices services, User user)
    {
        var window = new UnitWindow(new UnitWindowViewModel(services, user));
        var owner = Application.Current?.MainWindow;
        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }
}
