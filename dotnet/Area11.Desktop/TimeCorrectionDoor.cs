using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>The bill-time fix keeps the Bills page and its selection stable until it is closed.</summary>
internal static class TimeCorrectionDoor
{
    public static void Show(AppServices services, User user)
    {
        var window = new TimeCorrectionWindow(new TimeCorrectionViewModel(services, user));
        if (Application.Current?.MainWindow is Window owner && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }
}
